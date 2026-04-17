using Microsoft.Extensions.DependencyInjection;

namespace ClinicalIntelligence.Presentation;

/// <summary>
/// Registers all ClinicalIntelligence module services into the DI container.
/// Called once from Program.cs during application startup.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClinicalIntelligenceModule(this IServiceCollection services)
    {
        // Application and data services for ClinicalIntelligence will be registered here
        // as they are implemented in subsequent tasks.
        return services;
    }
}
