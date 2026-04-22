namespace Admin.Application.Analytics.Dto;

/// <summary>
/// Live infrastructure health snapshot for the analytics dashboard (US_033, AC-4, TR-017).
/// No PHI — operational metrics only (OWASP A01 + HIPAA).
/// </summary>
public sealed record SystemHealthDto(
    double ApiLatencyP50Ms,
    double ApiLatencyP95Ms,
    double ApiLatencyP99Ms,
    int DbPoolUsagePct,
    int CacheHitRatioPct,
    string AiGatewayStatus);
