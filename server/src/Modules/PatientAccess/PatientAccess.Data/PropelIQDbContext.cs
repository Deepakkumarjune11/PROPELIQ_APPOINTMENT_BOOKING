using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using PatientAccess.Data.Configurations;
using PatientAccess.Data.Converters;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data;

/// <summary>
/// Primary EF Core DbContext for the PropelIQ platform.
/// Hosted in PatientAccess.Data because the initial migration lives here;
/// all cross-module entities are mapped within this single context (shared schema per DR-001–DR-010).
/// </summary>
public sealed class PropelIQDbContext : DbContext
{
    private readonly PhiEncryptedConverter? _phiConverter;
    private readonly PhiEncryptedNullableConverter? _phiNullableConverter;

    /// <summary>
    /// Runtime constructor — <paramref name="dataProtectionProvider"/> enables PHI column encryption
    /// for all DR-015 columns (AC-1, TR-022). Null at design-time (migrations tooling).
    /// </summary>
    public PropelIQDbContext(
        DbContextOptions<PropelIQDbContext> options,
        IDataProtectionProvider? dataProtectionProvider = null) : base(options)
    {
        if (dataProtectionProvider is not null)
        {
            // Single purpose string — all PHI columns share one key ring (DR-015 / TR-022).
            var protector = dataProtectionProvider.CreateProtector("phi-data-at-rest");
            _phiConverter         = new PhiEncryptedConverter(protector);
            _phiNullableConverter = new PhiEncryptedNullableConverter(protector);
        }
    }

    // ── Patient Access ──────────────────────────────────────────────────────
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<IntakeResponse> IntakeResponses => Set<IntakeResponse>();

    // ── Staff / Admin ───────────────────────────────────────────────────────
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<Admin> Admins => Set<Admin>();

    // ── Clinical Intelligence ───────────────────────────────────────────────
    public DbSet<ClinicalDocument> ClinicalDocuments => Set<ClinicalDocument>();
    public DbSet<DocumentChunkEmbedding> DocumentChunkEmbeddings => Set<DocumentChunkEmbedding>();
    public DbSet<ExtractedFact> ExtractedFacts => Set<ExtractedFact>();
    public DbSet<PatientView360> PatientViews360 => Set<PatientView360>();
    public DbSet<CodeSuggestion> CodeSuggestions => Set<CodeSuggestion>();

    // ── Audit (append-only — DR-012) ────────────────────────────────────────
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // ── AI Audit (sanitised metadata only — AIR-S03, DR-012) ───────────────
    public DbSet<AIPromptLog> AiPromptLogs => Set<AIPromptLog>();
    // ── RAG Access Control (AIR-S02, US_029/task_002) ───────────────────────────
    public DbSet<DocumentAccessGrant> DocumentAccessGrants => Set<DocumentAccessGrant>();
    // ── Communication (SMS / email audit + PDF bytes — FR-007, TR-014) ──────
    public DbSet<CommunicationLog> CommunicationLogs => Set<CommunicationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enable pgvector extension — TR-003 / DR-016
        modelBuilder.HasPostgresExtension("vector");

        // PHI-encrypted configurations: pass converters when available (runtime) or null (EF tooling).
        // EF tooling path (converters null) skips encryption — migrations inspect schema, not values.
        modelBuilder.ApplyConfiguration(new PatientConfiguration(_phiConverter, _phiNullableConverter));
        modelBuilder.ApplyConfiguration(new AppointmentConfiguration());
        modelBuilder.ApplyConfiguration(new IntakeResponseConfiguration(_phiConverter));
        modelBuilder.ApplyConfiguration(new ClinicalDocumentConfiguration(_phiConverter));
        modelBuilder.ApplyConfiguration(new DocumentChunkEmbeddingConfiguration());
        modelBuilder.ApplyConfiguration(new ExtractedFactConfiguration(_phiConverter));
        modelBuilder.ApplyConfiguration(new StaffConfiguration());
        modelBuilder.ApplyConfiguration(new AdminConfiguration());
        modelBuilder.ApplyConfiguration(new PatientView360Configuration(_phiConverter));
        modelBuilder.ApplyConfiguration(new CodeSuggestionConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new AIPromptLogConfiguration());
        modelBuilder.ApplyConfiguration(new CommunicationLogConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentAccessGrantConfiguration());
    }

    // ── Entity Configurations ────────────────────────────────────────────────

}
