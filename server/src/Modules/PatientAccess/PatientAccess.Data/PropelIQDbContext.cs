using Microsoft.EntityFrameworkCore;
using PatientAccess.Data.Configurations;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data;

/// <summary>
/// Primary EF Core DbContext for the PropelIQ platform.
/// Hosted in PatientAccess.Data because the initial migration lives here;
/// all cross-module entities are mapped within this single context (shared schema per DR-001–DR-010).
/// </summary>
public sealed class PropelIQDbContext : DbContext
{
    public PropelIQDbContext(DbContextOptions<PropelIQDbContext> options) : base(options) { }

    // ── Patient Access ──────────────────────────────────────────────────────
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<IntakeResponse> IntakeResponses => Set<IntakeResponse>();

    // ── Staff / Admin ───────────────────────────────────────────────────────
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<Admin> Admins => Set<Admin>();

    // ── Clinical Intelligence ───────────────────────────────────────────────
    public DbSet<ClinicalDocument> ClinicalDocuments => Set<ClinicalDocument>();
    public DbSet<ExtractedFact> ExtractedFacts => Set<ExtractedFact>();
    public DbSet<PatientView360> PatientViews360 => Set<PatientView360>();
    public DbSet<CodeSuggestion> CodeSuggestions => Set<CodeSuggestion>();

    // ── Audit (append-only — DR-012) ────────────────────────────────────────
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enable pgvector extension — TR-003 / DR-016
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfiguration(new PatientConfiguration());
        modelBuilder.ApplyConfiguration(new AppointmentConfiguration());
        modelBuilder.ApplyConfiguration(new IntakeResponseConfiguration());
        modelBuilder.ApplyConfiguration(new ClinicalDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new ExtractedFactConfiguration());
        modelBuilder.ApplyConfiguration(new StaffConfiguration());
        modelBuilder.ApplyConfiguration(new AdminConfiguration());
        modelBuilder.ApplyConfiguration(new PatientView360Configuration());
        modelBuilder.ApplyConfiguration(new CodeSuggestionConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
    }

    // ── Entity Configurations ────────────────────────────────────────────────

}
