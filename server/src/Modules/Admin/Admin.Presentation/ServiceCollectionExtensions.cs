using Microsoft.Extensions.DependencyInjection;

namespace Admin.Presentation;

/// <summary>
/// Registers all Admin module services into the DI container.
/// Called once from Program.cs during application startup.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAdminModule(this IServiceCollection services)
    {
        // Application and data services for Admin will be registered here
        // as they are implemented in subsequent tasks.
        return services;
    }
}
