namespace PatientAccess.Application.Services;

/// <summary>
/// Data transfer object carrying all appointment details needed to generate a
/// PDF confirmation document and compose the notification email body.
/// Properties use public setters so Hangfire (Newtonsoft.Json) can deserialize
/// the object when the job is replayed from storage.
/// </summary>
public sealed class AppointmentConfirmationDetails
{
    public Guid AppointmentId { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientEmail { get; set; } = string.Empty;
    public string PatientPhone { get; set; } = string.Empty;
    public DateTime SlotDatetime { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string? VisitReason { get; set; }
    public string ClinicName { get; set; } = string.Empty;
    public string ClinicPhone { get; set; } = string.Empty;
}
