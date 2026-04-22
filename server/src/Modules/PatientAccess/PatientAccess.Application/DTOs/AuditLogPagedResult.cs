namespace PatientAccess.Application.DTOs;

/// <summary>
/// Paginated wrapper for <see cref="AuditLogDto"/> results (US_026, AC-3).
/// </summary>
public sealed record AuditLogPagedResult(
    IReadOnlyList<AuditLogDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages);
