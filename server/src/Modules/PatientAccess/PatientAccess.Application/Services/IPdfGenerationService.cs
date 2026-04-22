namespace PatientAccess.Application.Services;

/// <summary>
/// Generates a PDF appointment confirmation document from booking details (TR-014).
/// </summary>
public interface IPdfGenerationService
{
    /// <summary>
    /// Generates a single-page appointment confirmation PDF.
    /// </summary>
    /// <param name="details">Appointment and patient details to render.</param>
    /// <returns>Raw PDF bytes suitable for attaching to an email or serving over HTTP.</returns>
    byte[] Generate(AppointmentConfirmationDetails details);
}
