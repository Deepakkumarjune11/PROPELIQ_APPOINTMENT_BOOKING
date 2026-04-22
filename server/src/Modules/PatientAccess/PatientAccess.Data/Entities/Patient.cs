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

    /// <summary>
    /// Bcrypt/PBKDF2 hash of the patient's password. Null until the patient sets a password.
    /// Never returned in any API response (OWASP A02).
    /// </summary>
    public string? AuthCredentials { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>Administrative department assignment for RAG access scoping (AIR-S02). NOT PHI.</summary>
    public string? Department { get; private set; }

    public void SetDepartment(string? department)
        => Department = department is { Length: > 100 }
            ? throw new ArgumentException("Department name exceeds 100 characters.", nameof(department))
            : department;

    // Navigation
    public ICollection<Appointment> Appointments { get; set; } = [];
    public ICollection<IntakeResponse> IntakeResponses { get; set; } = [];
    public ICollection<ClinicalDocument> ClinicalDocuments { get; set; } = [];
    public PatientView360? PatientView360 { get; set; }
    public ICollection<CodeSuggestion> CodeSuggestions { get; set; } = [];
}
