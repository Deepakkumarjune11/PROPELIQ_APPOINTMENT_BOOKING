namespace PatientAccess.Application.Patients.Dtos;

/// <summary>
/// Patient search result for staff autocomplete (US_016, AC-2).
/// </summary>
/// <param name="Id">Patient record identifier.</param>
/// <param name="FullName">Patient display name.</param>
/// <param name="Email">Patient email address.</param>
/// <param name="Phone">Patient phone number (E.164).</param>
public sealed record PatientSearchResultDto(
    Guid   Id,
    string FullName,
    string Email,
    string Phone);
