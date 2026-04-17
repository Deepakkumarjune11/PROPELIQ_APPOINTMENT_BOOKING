using System.Reflection;
using Admin.Presentation;
using ClinicalIntelligence.Presentation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using PatientAccess.Application.Infrastructure;
using Microsoft.AspNetCore.Identity;
using PatientAccess.Data;
using PatientAccess.Data.Interceptors;
using PatientAccess.Data.Seeding;
using PatientAccess.Presentation;
using PropelIQ.Api;
using PropelIQ.Api.HealthCheck;
using PropelIQ.Api.Infrastructure.Caching;
using Serilog;
using StackExchange.Redis;

// Bootstrap Serilog immediately so that ConfigurationValidator errors are captured
// before the host is fully built.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting PropelIQ API host");

    var builder = WebApplication.CreateBuilder(args);

    // Structured logging via Serilog — replaces default Microsoft.Extensions.Logging
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console());

    // Validate required configuration keys before registering services.
    // Throws InvalidOperationException (logged) if any required key is absent.
    ConfigurationValidator.Validate(builder.Configuration);

    // Redis — IConnectionMultiplexer singleton shared across the app lifetime (TR-004)
    var redisConnectionString = builder.Configuration["Redis:ConnectionString"]!;
    var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
    redisOptions.AbortOnConnectFail = false;
    redisOptions.ConnectRetry       = 3;
    redisOptions.ConnectTimeout     = 5_000;
    redisOptions.SyncTimeout        = 3_000;
    builder.Services.AddSingleton<IConnectionMultiplexer>(
        ConnectionMultiplexer.Connect(redisOptions));

    // IDistributedCache backed by Redis — for ASP.NET Core session and built-in cache consumers
    builder.Services.AddStackExchangeRedisCache(options =>
        options.Configuration = redisConnectionString);

    // Scoped ICacheService for application-layer cache operations
    builder.Services.AddScoped<ICacheService, RedisCacheService>();

    // --- Service Registration ---

    // Bounded context module DI registrations
    builder.Services.AddPatientAccessModule();
    builder.Services.AddClinicalIntelligenceModule();
    builder.Services.AddAdminModule();

    // AuditLog immutability interceptor registered as singleton for DI override in tests (DR-008)
    builder.Services.AddSingleton<AuditLogImmutabilityInterceptor>();

    // Password hashers for Staff and Admin — used by seeder and auth service (OWASP A02)
    builder.Services.AddScoped<IPasswordHasher<PatientAccess.Data.Entities.Staff>, PasswordHasher<PatientAccess.Data.Entities.Staff>>();
    builder.Services.AddScoped<IPasswordHasher<PatientAccess.Data.Entities.Admin>, PasswordHasher<PatientAccess.Data.Entities.Admin>>();

    // Seed data — only in Development or when SeedData:Enabled is explicitly true
    var seedEnabled = builder.Configuration.GetValue<bool>("SeedData:Enabled",
        defaultValue: builder.Environment.IsDevelopment());

    if (seedEnabled)
        builder.Services.AddScoped<IDataSeeder, DevelopmentDataSeeder>();
    else
        builder.Services.AddScoped<IDataSeeder, NoOpDataSeeder>();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // OpenAPI/Swagger — TR-011: available in all environments, not just Development
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title       = "PropelIQ API",
            Version     = "v1",
            Description = "Appointment Booking and Clinical Intelligence Platform REST API.",
        });

        // Integrate XML doc comments from the compiled documentation file
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        }
    });

    // Health checks — PostgreSQL + Redis with Degraded (not Unhealthy) on individual failure
    // so the app stays alive when one dependency is temporarily unavailable (AC-4 / edge case)
    builder.Services.AddHealthChecks()
        .AddNpgSql(
            builder.Configuration.GetConnectionString("DefaultConnection")!,
            name: "postgresql",
            failureStatus: HealthStatus.Degraded,
            tags: ["database", "readiness"])
        .AddRedis(
            redisConnectionString,
            name: "redis",
            failureStatus: HealthStatus.Degraded,
            tags: ["cache", "readiness"]);

    // EF Core DbContext — Npgsql provider, retry-on-failure, pool size TR-021
    builder.Services.AddDbContext<PropelIQDbContext>((sp, options) =>
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            npgsql =>
            {
                npgsql.SetPostgresVersion(15, 0);
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                npgsql.MigrationsAssembly("PatientAccess.Data");
            })
        .AddInterceptors(sp.GetRequiredService<AuditLogImmutabilityInterceptor>()));

    // Central problem details RFC 7807 response formatting
    builder.Services.AddProblemDetails();

    // --- Middleware Pipeline ---

    var app = builder.Build();

    // Run seed data pipeline once at startup before accepting HTTP traffic
    using (var scope = app.Services.CreateScope())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
        await seeder.SeedAsync();
    }

    app.UseExceptionHandler();

    // Swagger middleware — not environment-gated per TR-011
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "PropelIQ API V1"));

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    // Health check endpoint — returns HTTP 200 (Healthy) or 503 (Unhealthy)
    // Response includes environment name, assembly version, and UTC timestamp (AC-5)
    var appVersion = Assembly
        .GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "unknown";

    app.MapHealthChecks("/api/health", new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";

            var response = new HealthCheckResponse(
                Status:      report.Status.ToString(),
                Environment: app.Environment.EnvironmentName,
                Version:     appVersion,
                Timestamp:   DateTime.UtcNow,
                Checks:      report.Entries.ToDictionary(
                                 e => e.Key,
                                 e => e.Value.Status.ToString())
            );

            await context.Response.WriteAsJsonAsync(response);
        },
    });

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "PropelIQ API host terminated unexpectedly");
    Environment.Exit(1);
}
finally
{
    // Flush and close all Serilog sinks before the process exits
    await Log.CloseAndFlushAsync();
}
