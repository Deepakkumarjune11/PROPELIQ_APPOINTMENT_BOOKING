using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PatientAccess.Application.Exceptions;
using PatientAccess.Application.Patients.Commands.CreatePatientByStaff;
using PatientAccess.Application.Patients.Dtos;
using PatientAccess.Application.Repositories;
using PatientAccess.Data.Entities;
using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPatientStaffRepository"/>.
/// </summary>
public sealed class PatientStaffRepository : IPatientStaffRepository
{
    private const int MaxResults = 10;

    private readonly PropelIQDbContext _db;

    public PatientStaffRepository(PropelIQDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<PatientSearchResultDto>> SearchByEmailOrPhoneAsync(
        string            query,
        CancellationToken cancellationToken = default)
    {
        var pattern = $"%{query}%";

        return await _db.Patients
            .AsNoTracking()
            .Where(p => !p.IsDeleted &&
                        (EF.Functions.ILike(p.Email, pattern) ||
                         EF.Functions.ILike(p.Phone, pattern)))
            .OrderBy(p => p.Name)
            .Take(MaxResults)
            .Select(p => new PatientSearchResultDto(p.Id, p.Name, p.Email, p.Phone))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PatientSearchResultDto> CreatePatientAsync(
        CreatePatientByStaffCommand command,
        CancellationToken           cancellationToken = default)
    {
        // Email uniqueness guard (edge case → 409).
        bool exists = await _db.Patients
            .AnyAsync(p => p.Email == command.Email && !p.IsDeleted, cancellationToken);

        if (exists)
            throw new ConflictException("A patient with this email already exists.");

        var patient = new Patient
        {
            Id        = Guid.NewGuid(),
            Name      = command.FullName,
            Email     = command.Email,
            Phone     = command.Phone,
            Dob       = DateOnly.MinValue,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.Patients.Add(patient);

        _db.AuditLogs.Add(new AuditLog
        {
            Id             = Guid.NewGuid(),
            ActorId        = command.StaffId,
            ActorType      = AuditActorType.Staff,
            ActionType     = AuditActionType.PatientDataAccess,
            TargetEntityId = patient.Id,
            OccurredAt     = DateTime.UtcNow,
            Details        = JsonSerializer.Serialize(new { action = "PatientCreatedByStaff", email = command.Email }),
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new PatientSearchResultDto(patient.Id, patient.Name, patient.Email, patient.Phone);
    }
}
