using Hangfire;
using Hangfire.PostgreSql;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PatientAccess.Application.AI;
using PatientAccess.Application.Commands.RegisterForAppointment;
using PatientAccess.Application.Configuration;
using PatientAccess.Application.Infrastructure;
using PatientAccess.Application.Jobs;
using PatientAccess.Application.Queries.GetAvailability;
using PatientAccess.Application.Repositories;
using PatientAccess.Application.Services;
using PatientAccess.Application.Shared;
using PatientAccess.Data.Repositories;
using PatientAccess.Presentation.ExceptionHandling;
using PatientAccess.Presentation.Services;

namespace PatientAccess.Presentation;

/// <summary>
/// Registers all PatientAccess module services into the DI container.
/// Called once from Program.cs during application startup.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPatientAccessModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // MediatR — scans PatientAccess.Application assembly for IRequestHandler<,> implementations.
        // Covers both GetAvailabilityHandler (US_009) and RegisterForAppointmentHandler (US_010).
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(GetAvailabilityHandler).Assembly));

        // Repository bindings — Application layer depends on interfaces; Data provides implementations.
        services.AddScoped<IAvailabilityRepository, AvailabilityRepository>();
        services.AddScoped<IAppointmentRegistrationRepository, AppointmentRegistrationRepository>();
        services.AddScoped<IIntakeSubmissionRepository, IntakeSubmissionRepository>();
        services.AddScoped<IAIPromptLogRepository, AIPromptLogRepository>();

        // .NET Data Protection API — encrypts PHI columns at rest (DR-015, OWASP A02).
        // SetApplicationName ensures keys produced in different environments are cross-compatible
        // when the same key ring is shared (e.g., multiple API instances behind a load balancer).
        services.AddDataProtection()
                .SetApplicationName("PropelIQ");

        // Insurance validation options — loaded from appsettings.json:InsuranceReference (FR-009).
        services.Configure<InsuranceReferenceOptions>(
            configuration.GetSection(InsuranceReferenceOptions.SectionName));

        // Insurance validation service — in-memory matching against config-bound reference set.
        services.AddScoped<IInsuranceValidationService, InsuranceValidationService>();

        // No-show risk scoring — deterministic, pure rule engine; singleton (no I/O, no mutable state).
        services.Configure<NoShowRiskOptions>(configuration.GetSection(NoShowRiskOptions.SectionName));
        services.AddSingleton<INoShowRiskScoringService, NoShowRiskScoringService>();

        // Azure OpenAI options — sourced from AzureOpenAI config section; API key injected via
        // environment variable / Azure Key Vault in production (OWASP A02).
        services.Configure<AzureOpenAIOptions>(configuration.GetSection(AzureOpenAIOptions.SectionName));

        // AI intake service — scoped so it can consume scoped IAIPromptLogRepository (AIR-S03).
        // AzureOpenAiIntakeService gracefully throws AiServiceUnavailableException when endpoint
        // is not configured, enabling the conversational intake service to fall back to manual mode.
        services.AddScoped<IAiIntakeService, AzureOpenAiIntakeService>();

        // Conversational intake orchestration service — manages Redis history, delegates to IAiIntakeService.
        services.AddScoped<IConversationalIntakeService, ConversationalIntakeService>();

        // Exception handler — maps NotFoundException → 404, ConflictException → 409 (RFC 7807).
        services.AddExceptionHandler<PatientAccessExceptionHandler>();

        // PDF generation — singleton; PDFsharp has no mutable shared state (TR-014).
        services.AddSingleton<IPdfGenerationService, PdfSharpConfirmationService>();

        // Communication log repository — scoped (wraps PropelIQDbContext which is scoped).
        services.AddScoped<ICommunicationLogRepository, CommunicationLogRepository>();

        // Watchlist repository — preferred slot swap data access (US_015).
        services.AddScoped<IWatchlistRepository, WatchlistRepository>();

        // Slot swap repository — SERIALIZABLE transaction + FOR UPDATE row-lock (US_015, AC-3).
        services.AddScoped<ISlotSwapRepository, SlotSwapRepository>();

        // Slot swap service — orchestrates swap attempts and enqueues post-commit notifications.
        services.AddScoped<SlotSwapService>();

        // Patient ownership validator — RBAC guard for appointment-scoped endpoints (US_015, OWASP A01).
        services.AddScoped<IPatientOwnershipValidator, PatientOwnershipValidator>();

        // Walk-in booking repositories — staff walk-in flow (US_016).
        services.AddScoped<IPatientStaffRepository, PatientStaffRepository>();
        services.AddScoped<IWalkInBookingRepository, WalkInBookingRepository>();
        services.AddScoped<IStaffDashboardRepository, StaffDashboardRepository>();

        // Queue management — same-day queue CRUD + SignalR broadcast (US_017).
        services.AddScoped<IQueueRepository, QueueRepository>();
        services.AddScoped<IQueueBroadcastService, SignalRQueueBroadcastService>();

        // Twilio options — AccountSid, AuthToken, FromNumber (OWASP A02: empty in appsettings; env-var override in prod).
        services.Configure<TwilioOptions>(configuration.GetSection(TwilioOptions.SectionName));

        // SendGrid options — ApiKey, FromEmail (OWASP A02: same pattern).
        services.Configure<SendGridOptions>(configuration.GetSection(SendGridOptions.SectionName));

        // Hangfire — background job processing with PostgreSQL storage (TR-009).
        // Two queues: "critical" (confirmations, PDF) and "default" (24h reminders).
        // Dashboard is NOT exposed here — middleware is added in Program.cs (dev-only).
        services.AddHangfire(c => c
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                o => o.UseNpgsqlConnection(
                    configuration.GetConnectionString("DefaultConnection")!),
                new PostgreSqlStorageOptions { QueuePollInterval = TimeSpan.FromSeconds(5) }));

        services.AddHangfireServer(opts =>
            opts.Queues = new[] { "critical", "default", "document-extraction", "fact-extraction", "view360-update", "conflict-detection", "code-suggestion" });

        // IHttpClientFactory — used by GoogleCalendarService + OutlookCalendarService for token
        // exchange and API calls. AddHttpClient() also configures HttpClientFactory globally.
        services.AddHttpClient();

        // Calendar options — OAuth client IDs/secrets sourced from IConfiguration (OWASP A02).
        services.Configure<CalendarOptions>(configuration.GetSection(CalendarOptions.SectionName));

        // Calendar appointment repository — read-only; keyed calendar services depend on it.
        services.AddScoped<ICalendarAppointmentRepository, CalendarAppointmentRepository>();

        // Keyed calendar services — resolved by provider key in CalendarController (TR-012).
        // Scoped lifetime so PropelIQDbContext (scoped) can be consumed by the repository.
        services.AddKeyedScoped<ICalendarService, GoogleCalendarService>("google");
        services.AddKeyedScoped<ICalendarService, OutlookCalendarService>("outlook");

        // Audit logger — IHttpContextAccessor captures caller IP; AuditLogger stages entries
        // on the DbContext change tracker without calling SaveChangesAsync (AC-5, US_026).
        // AddHttpContextAccessor() is idempotent — safe to call even if registered elsewhere.
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditLogger, PatientAccess.Data.Services.AuditLogger>();

        return services;
    }
}

