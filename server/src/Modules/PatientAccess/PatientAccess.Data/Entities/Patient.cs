namespace PatientAccess.Data.Entities;

public class Patient
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly Dob { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? InsuranceProvider { get; set; }
    public string? InsuranceMemberId { get; set; }
    public string? InsuranceStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Navigation
    public ICollection<Appointment> Appointments { get; set; } = [];
    public ICollection<IntakeResponse> IntakeResponses { get; set; } = [];
    public ICollection<ClinicalDocument> ClinicalDocuments { get; set; } = [];
    public PatientView360? PatientView360 { get; set; }
    public ICollection<CodeSuggestion> CodeSuggestions { get; set; } = [];
}
