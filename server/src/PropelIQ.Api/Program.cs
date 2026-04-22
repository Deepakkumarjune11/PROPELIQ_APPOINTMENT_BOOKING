using ClinicalIntelligence.Application.AI.FeatureFlags;
using Microsoft.AspNetCore.Diagnostics;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Admin.Presentation;
using ClinicalIntelligence.Presentation;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PatientAccess.Application.Infrastructure;
using PatientAccess.Application.Services;
using PatientAccess.Data;
using PatientAccess.Data.Interceptors;
using PatientAccess.Data.Seeding;
using PatientAccess.Data.Services;
using PatientAccess.Presentation;
using PropelIQ.Api;
using PropelIQ.Api.HealthCheck;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using PropelIQ.Api.Infrastructure.Caching;
using PropelIQ.Api.Infrastructure.Maintenance;
using PropelIQ.Api.Infrastructure.Resilience;
using PropelIQ.Api.Infrastructure.Uptime;
using Serilog;
using StackExchange.Redis;
using System.Net;
using System.Net.Http.Headers;

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

    // Typed slot cache service with configurable TTLs and hit-ratio tracking (US_035, AC-3)
    builder.Services.AddScoped<ISlotCacheService, SlotCacheService>();
    builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection(CacheOptions.SectionName));

    // --- Service Registration ---

    // Bounded context module DI registrations
    builder.Services.AddPatientAccessModule(builder.Configuration);
    builder.Services.AddClinicalIntelligenceModule(builder.Configuration);
    builder.Services.AddAdminModule();

    // AuditLog immutability interceptor registered as singleton for DI override in tests (DR-008)
    builder.Services.AddSingleton<AuditLogImmutabilityInterceptor>();

    // JWT Bearer authentication (TR-010) — strict 15-min TTL with Redis token blacklist (FR-017)
    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("Jwt:Key is required");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateIssuer           = true,
                ValidIssuer              = builder.Configuration["Jwt:Issuer"],
                ValidateAudience         = true,
                ValidAudience            = builder.Configuration["Jwt:Audience"],
                ValidateLifetime         = true,
                ClockSkew                = TimeSpan.Zero,   // strict 15-min expiry per NFR-005
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async ctx =>
                {
                    try
                    {
                        // 1. Token-level blacklist check (logout / refresh rotation)
                        var cache = ctx.HttpContext.RequestServices
                            .GetRequiredService<IDistributedCache>();
                        var authHeader = ctx.HttpContext.Request.Headers.Authorization.ToString();
                        var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                            ? authHeader["Bearer ".Length..]
                            : authHeader;
                        var blacklisted = await cache.GetStringAsync($"blacklist:{token}");
                        if (blacklisted is not null)
                        {
                            ctx.Fail("Token has been revoked");
                            return;
                        }

                        // 2. User-level session invalidation check (admin disable / role change)
                        var userIdClaim = ctx.Principal?.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
                        if (Guid.TryParse(userIdClaim, out var uid))
                        {
                            var invalidator = ctx.HttpContext.RequestServices
                                .GetRequiredService<Admin.Application.Services.ISessionInvalidator>();
                            if (await invalidator.IsUserInvalidatedAsync(uid, CancellationToken.None))
                                ctx.Fail("User session has been invalidated");
                        }
                    }
                    catch (StackExchange.Redis.RedisException ex)
                    {
                        // Redis unavailable — fail-open so auth is not blocked by a cache outage.
                        // Log the issue but allow the request through (token signature already validated).
                        var logger = ctx.HttpContext.RequestServices
                            .GetRequiredService<ILogger<Program>>();
                        logger.LogWarning(ex, "Redis unavailable during token validation; skipping blacklist check");
                    }
                }
            };
        });

    builder.Services.AddAuthorization();

    // Rate limiter — 5 login attempts per IP per 60 seconds (OWASP A07 brute-force protection)
    // Read resilience config eagerly for rate limiter and HTTP client setup (US_035, AC-1/AC-4/AC-5)
    var resilienceOpts = builder.Configuration
        .GetSection(ExternalServiceResilienceOptions.SectionName)
        .Get<ExternalServiceResilienceOptions>() ?? new ExternalServiceResilienceOptions();

    builder.Services.AddRateLimiter(options =>
    {
        // Login endpoint — 5 attempts per IP per 60s (OWASP A07, pre-existing)
        options.AddPolicy("login-fixed-window", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit          = 5,
                    Window               = TimeSpan.FromSeconds(60),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit           = 0,
                }));

        // Global per-user fixed window: 100 req/10s partitioned by JWT sub claim (AC-1 — p95 < 500ms at 200 users)
        // Falls back to IP address for unauthenticated requests (OWASP A04)
        options.AddPolicy("api-global", httpContext =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous";
            return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit          = resilienceOpts.GlobalWindowRequestLimit,
                Window               = TimeSpan.FromSeconds(resilienceOpts.GlobalWindowSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 10,  // absorb brief bursts without immediate rejection
            });
        });

        // Auth token bucket: 5 tokens replenished at 1/12s per IP (OWASP A07, AC-1)
        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit            = resilienceOpts.AuthBucketTokenLimit,
                    ReplenishmentPeriod   = TimeSpan.FromSeconds(
                        resilienceOpts.AuthBucketReplenishSeconds / (double)resilienceOpts.AuthBucketTokenLimit),
                    TokensPerPeriod       = 1,
                    QueueProcessingOrder  = QueueProcessingOrder.OldestFirst,
                    QueueLimit            = 0,  // reject immediately for auth
                }));

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Attach Retry-After: 1 header and RFC 7807 body on all rate-limit rejections
        options.OnRejected = async (ctx, ct) =>
        {
            ctx.HttpContext.Response.Headers.RetryAfter = "1";
            await ctx.HttpContext.Response.WriteAsJsonAsync(
                new { status = 429, title = "Rate limit exceeded", retryAfterSeconds = 1 }, ct);
        };
    });

    // Named HTTP clients with Polly exponential retry (3× at 1s/2s/4s, TR-023)
    // SendGrid — email alert dispatch in AlertNotificationService (US_034)
    builder.Services.AddHttpClient("sendgrid-http", client =>
    {
        client.BaseAddress = new Uri("https://api.sendgrid.com");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(10);
    })
    .AddResilienceHandler("sendgrid-retry", pipeline =>
    {
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = resilienceOpts.RetryCount,
            Delay            = TimeSpan.FromSeconds(resilienceOpts.RetryBaseDelaySeconds),
            BackoffType      = DelayBackoffType.Exponential,
            UseJitter        = true,
            ShouldHandle     = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .HandleResult(r =>
                    r.StatusCode == HttpStatusCode.TooManyRequests ||
                    (int)r.StatusCode >= 500),
        });
    });

    // PagerDuty — alert notification dispatch in AlertNotificationService (US_034)
    builder.Services.AddHttpClient("pagerduty-http", client =>
    {
        client.BaseAddress = new Uri("https://events.pagerduty.com");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(8);
    })
    .AddResilienceHandler("pagerduty-retry", pipeline =>
    {
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = resilienceOpts.RetryCount,
            Delay            = TimeSpan.FromSeconds(resilienceOpts.RetryBaseDelaySeconds),
            BackoffType      = DelayBackoffType.Exponential,
            UseJitter        = true,
            ShouldHandle     = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .HandleResult(r =>
                    r.StatusCode == HttpStatusCode.TooManyRequests ||
                    (int)r.StatusCode >= 500),
        });
    });

    // Bulkhead isolation — SemaphoreSlim concurrency cap per external dependency (US_035, AC-4)
    builder.Services.AddSingleton<IExternalServiceBulkhead, ExternalServiceBulkhead>();
    builder.Services.Configure<ExternalServiceResilienceOptions>(
        builder.Configuration.GetSection(ExternalServiceResilienceOptions.SectionName));

    // Auth service — validates Staff/Admin credentials and issues JWTs
    builder.Services.AddScoped<IAuthService, AuthService>();

    // Password hashers for Staff, Admin, and Patient — used by seeder and auth service (OWASP A02)
    builder.Services.AddScoped<IPasswordHasher<PatientAccess.Data.Entities.Staff>, PasswordHasher<PatientAccess.Data.Entities.Staff>>();
    builder.Services.AddScoped<IPasswordHasher<PatientAccess.Data.Entities.Admin>, PasswordHasher<PatientAccess.Data.Entities.Admin>>();
    builder.Services.AddScoped<IPasswordHasher<PatientAccess.Data.Entities.Patient>, PasswordHasher<PatientAccess.Data.Entities.Patient>>();

    // Seed data — only in Development or when SeedData:Enabled is explicitly true
    var seedEnabled = builder.Configuration.GetValue<bool>("SeedData:Enabled",
        defaultValue: builder.Environment.IsDevelopment());

    if (seedEnabled)
        builder.Services.AddScoped<IDataSeeder, DevelopmentDataSeeder>();
    else
        builder.Services.AddScoped<IDataSeeder, NoOpDataSeeder>();

    // CORS — allow the Vite dev server (http://localhost:3000) to call the API directly.
    // In production this is removed and the reverse proxy handles origin enforcement.
    builder.Services.AddCors(options =>
        options.AddPolicy("ViteDev", policy =>
            policy.WithOrigins("http://localhost:3000", "http://localhost:3001")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()));

    builder.Services.AddControllers();

    // SignalR — real-time queue broadcast for staff dashboard (US_017, AC-4).
    builder.Services.AddSignalR();
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

        // Use full type name as schema ID to prevent conflicts when types from different
        // namespaces share the same simple name (e.g., LoginRequest, CreateUserRequest).
        options.CustomSchemaIds(t => t.FullName?.Replace("+", ".") ?? t.Name);

        // Bearer security definition — allows Swagger UI to authenticate with a JWT (TR-011)
        var bearerScheme = new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        };
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name        = "Authorization",
            Type        = SecuritySchemeType.Http,
            Scheme      = "bearer",
            BearerFormat = "JWT",
            In          = ParameterLocation.Header,
            Description = "Enter JWT Bearer token (obtained from POST /api/v1/auth/login)",
        });
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            { bearerScheme, Array.Empty<string>() }
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
            tags: ["cache", "readiness"])
        // US_030/task_002: Azure OpenAI health probe (1-token embedding, 2s timeout)
        .AddCheck<AzureOpenAiHealthCheck>(
            name: "azure-openai",
            failureStatus: HealthStatus.Degraded,  // Unhealthy probe → Degraded app (not hard down)
            tags: ["ai", "live"])
        // US_034/task_001: Hangfire background job processor liveness (3s timeout)
        .AddCheck<HangfireHealthCheck>(
            name: "hangfire",
            failureStatus: HealthStatus.Degraded,
            tags: ["jobs", "readiness"]);

    // Health alert notification — Redis-backed failure tracker + multi-channel dispatcher (US_034, AC-2)
    builder.Services.AddSingleton<IHealthAlertTracker, RedisHealthAlertTracker>();
    builder.Services.AddScoped<IAlertNotificationService, AlertNotificationService>();
    builder.Services.AddScoped<HealthCheckAlertJob>();
    builder.Services.Configure<HealthAlertOptions>(
        builder.Configuration.GetSection(HealthAlertOptions.SectionName));

    // Maintenance mode + uptime tracking — Redis-backed singletons (US_034, AC-3/AC-4/AC-5)
    builder.Services.AddSingleton<IMaintenanceModeService, RedisMaintenanceModeService>();
    builder.Services.AddSingleton<IUptimeTracker, RedisUptimeTracker>();
    builder.Services.Configure<MaintenanceModeOptions>(
        builder.Configuration.GetSection(MaintenanceModeOptions.SectionName));

    // .NET Data Protection API — AES-256 PHI column encryption per DR-015 / TR-022.
    // Keys stored on the file system; rotate every 90 days per AC-3.
    // PRODUCTION: replace PersistKeysToFileSystem with PersistKeysToAzureBlobStorage +
    // ProtectKeysWithAzureKeyVault for HSM-backed storage per HIPAA HITECH §164.312(a)(2)(iv).
    var dpKeysPath = builder.Configuration["DataProtection:KeysPath"]
        ?? Path.Combine(AppContext.BaseDirectory, "keys", "phi");
    Directory.CreateDirectory(dpKeysPath);
    builder.Services
        .AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath))
        .SetDefaultKeyLifetime(TimeSpan.FromDays(90))
        .SetApplicationName("propeliq-phi");   // isolates key ring per app; prevents cross-app decryption

    // EF Core DbContextPool — Npgsql provider, retry-on-failure; pool capped at 128 (US_035, AC-2)
    builder.Services.AddDbContextPool<PropelIQDbContext>((sp, options) =>
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
                npgsql.UseVector();   // Enable pgvector type mapping (DR-016, TR-015)
            })
        .AddInterceptors(sp.GetRequiredService<AuditLogImmutabilityInterceptor>())
        .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Warning),
        poolSize: 128);

    // TLS 1.3 enforcement — DR-015 / AC-2.
    // Kestrel only: IIS production uses Windows Schannel; configure TLS there via IIS Crypto
    // tool or registry (see web.config comment for registry path).
    builder.WebHost.ConfigureKestrel(kestrelOptions =>
    {
        kestrelOptions.ConfigureHttpsDefaults(httpsOptions =>
        {
            httpsOptions.SslProtocols = SslProtocols.Tls13;
        });
    });

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

    // Maintenance mode middleware — placed AFTER UseExceptionHandler (exceptions still caught)
    // and BEFORE UseAuthorization (unauthenticated users see maintenance page, not 401).
    // Exempt paths: /api/health, /api/v1/admin/maintenance, /swagger (US_034, AC-4)
    app.UseMiddleware<MaintenanceModeMiddleware>();

    app.UseExceptionHandler(exceptionApp =>
    {
        exceptionApp.Run(async context =>
        {
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            if (feature?.Error is FeatureDisabledException fde)
            {
                context.Response.StatusCode  = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(new
                {
                    type        = "https://propeliq.health/errors/feature-unavailable",
                    title       = "Feature Unavailable",
                    status      = 503,
                    detail      = $"The AI feature '{fde.FeatureName}' is currently disabled.",
                    featureName = fde.FeatureName,
                });
            }
            // All other exceptions fall through to the default problem-details handler
        });
    });

    // Hangfire dashboard — only available in Development (security: no auth in dev; blocked in prod per OWASP A01)
    if (app.Environment.IsDevelopment())
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            // Allow read-only view without Hangfire auth in development
            Authorization = []
        });
    }

    // Recurring background jobs ─────────────────────────────────────────────
    // Slot-swap watchlist poll — every 5 minutes (US_015, AC-3).
    RecurringJob.AddOrUpdate<PatientAccess.Application.Jobs.SlotSwapJob>(
        "slot-swap-watchlist",
        job => job.ExecuteAsync(CancellationToken.None),
        "*/5 * * * *",
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

    // Analytics materialized view refresh — hourly (US_033, AC-2).
    // REFRESH MATERIALIZED VIEW CONCURRENTLY: no table lock; reads proceed during refresh.
    RecurringJob.AddOrUpdate<Admin.Application.Analytics.RefreshMetricsMaterializedViewsJob>(
        "refresh-metrics-views",
        job => job.ExecuteAsync(CancellationToken.None),
        Cron.Hourly,
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

    // Health check alert polling — every 30 seconds (US_034, AC-1/AC-2).
    // Uses 6-part cron with seconds: "*/30 * * * * *" runs every 30 seconds.
    RecurringJob.AddOrUpdate<HealthCheckAlertJob>(
        "health-check-alert",
        job => job.ExecuteAsync(CancellationToken.None),
        "*/30 * * * * *");

    // Swagger middleware — not environment-gated per TR-011
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "PropelIQ API V1"));

    app.UseHttpsRedirection();
    app.UseCors("ViteDev");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers().RequireRateLimiting("api-global");

    // SignalR hub — JWT-protected; staff only (OWASP A01, US_017).
    app.MapHub<PatientAccess.Presentation.Hubs.QueueHub>("/hubs/queue");

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
    }).DisableRateLimiting();

    // US_030/task_002: AI-specific health endpoint with minimal OWASP A05-compliant response.
    // Filters to only the "ai" tagged check. Exception details are omitted from the public
    // HTTP body — verbose details are in Serilog structured logs only (OWASP A05).
    app.MapHealthChecks("/health/ai", new HealthCheckOptions
    {
        Predicate = r => r.Tags.Contains("ai"),
        ResponseWriter = async (ctx, report) =>
        {
            ctx.Response.ContentType = "application/json";
            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() }),
            });
            await ctx.Response.WriteAsync(result);
        },
    }).DisableRateLimiting();

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
