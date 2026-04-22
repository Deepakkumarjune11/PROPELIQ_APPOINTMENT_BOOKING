using Admin.Application.Analytics;
using Admin.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Admin.Presentation;

/// <summary>
/// Registers all Admin module services into the DI container.
/// Called once from Program.cs during application startup.
/// Note: IPasswordHasher&lt;Staff&gt; and IPasswordHasher&lt;Admin&gt; are already registered
/// in Program.cs and are available to UserManagementService via constructor injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAdminModule(this IServiceCollection services)
    {
        // User lifecycle management
        services.AddScoped<IUserManagementService, UserManagementService>();

        // Redis-backed session invalidation — used to force re-login after role/status change
        services.AddScoped<ISessionInvalidator, RedisSessionInvalidator>();

        // Analytics metrics — scoped: wraps PropelIQDbContext (scoped lifetime) (US_033)
        services.AddScoped<IMetricsQueryService, PostgresMetricsQueryService>();
        services.AddScoped<MetricsExportService>();

        return services;
    }
}
