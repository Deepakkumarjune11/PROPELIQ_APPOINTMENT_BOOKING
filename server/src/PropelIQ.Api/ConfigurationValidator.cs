using Serilog;

namespace PropelIQ.Api;

/// <summary>
/// Validates required configuration keys at application startup.
/// Logs all missing keys and throws if any are absent, preventing
/// silent misconfigurations from reaching production.
/// </summary>
public static class ConfigurationValidator
{
    private static readonly string[] RequiredKeys =
    [
        "ConnectionStrings:DefaultConnection",
        "Redis:ConnectionString",
        "Jwt:Key",
        "DataProtection:KeysPath",
    ];

    /// <summary>
    /// Checks that all required configuration keys are present and non-empty.
    /// Logs each missing key via Serilog and throws <see cref="InvalidOperationException"/>
    /// listing all missing keys if any are absent.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when one or more required configuration keys are missing.
    /// </exception>
    public static void Validate(IConfiguration configuration)
    {
        var missing = RequiredKeys
            .Where(key => string.IsNullOrWhiteSpace(configuration[key]))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        foreach (var key in missing)
        {
            Log.Error("Required configuration key is missing: {Key}", key);
        }

        var keyList = string.Join(", ", missing.Select(k => $"'{k}'"));
        throw new InvalidOperationException(
            $"Application startup failed: the following required configuration keys are missing or empty: {keyList}. " +
            "Set them in appsettings.json, environment variables, or user secrets before starting the application."
        );
    }
}
