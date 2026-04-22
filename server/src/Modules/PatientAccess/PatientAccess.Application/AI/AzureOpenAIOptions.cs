namespace PatientAccess.Application.AI;

/// <summary>
/// Configuration options for the Azure OpenAI service (AzureOpenAI section in appsettings).
/// API key must be sourced from environment variables or Azure Key Vault in production (OWASP A02).
/// </summary>
public sealed class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    /// <summary>Azure OpenAI resource endpoint, e.g. https://my-resource.openai.azure.com/</summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>API key — empty in source control; overridden by environment variable / Key Vault at runtime.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Name of the GPT-4 deployment in the Azure OpenAI resource.</summary>
    public string DeploymentName { get; init; } = "gpt-4-turbo";

    /// <summary>Azure OpenAI REST API version.</summary>
    public string ApiVersion { get; init; } = "2024-02-01";
}
