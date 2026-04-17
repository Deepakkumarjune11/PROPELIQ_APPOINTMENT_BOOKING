using Microsoft.Extensions.DependencyInjection;

namespace PatientAccess.Presentation;

/// <summary>
/// Registers all PatientAccess module services into the DI container.
/// Called once from Program.cs during application startup.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPatientAccessModule(this IServiceCollection services)
    {
        // Application and data services for PatientAccess will be registered here
        // as they are implemented in subsequent tasks.
        return services;
    }
}
