namespace ClinicalIntelligence.Application.AI;

/// <summary>
/// Configuration options for Azure OpenAI embedding generation.
/// Bound from the <c>AzureOpenAI</c> section in appsettings (same resource as GPT-4).
/// API key MUST be sourced from environment variables or Azure Key Vault in production (OWASP A02).
/// </summary>
public sealed class AzureOpenAIEmbeddingOptions
{
    public const string SectionName = "AzureOpenAI";

    /// <summary>Azure OpenAI resource endpoint, e.g. https://my-resource.openai.azure.com/</summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>API key — empty in source control; overridden by environment variable / Key Vault.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Deployment name for <c>text-embedding-3-small</c> in the Azure OpenAI resource.</summary>
    public string EmbeddingDeploymentName { get; init; } = "text-embedding-3-small";

    /// <summary>Deployment name for GPT-4 Turbo used in the fact extraction pipeline.</summary>
    public string ChatDeploymentName { get; init; } = "gpt-4-turbo";
}
