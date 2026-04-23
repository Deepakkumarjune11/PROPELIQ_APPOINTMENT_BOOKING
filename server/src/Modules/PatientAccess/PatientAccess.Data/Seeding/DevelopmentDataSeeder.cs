using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PatientAccess.Data.Entities;
using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Seeding;

public sealed class DevelopmentDataSeeder : IDataSeeder
{
    private readonly PropelIQDbContext _db;
    private readonly IPasswordHasher<Staff> _staffHasher;
    private readonly IPasswordHasher<Admin> _adminHasher;
    private readonly IPasswordHasher<Patient> _patientHasher;

    // Well-known development seed password — documented in developer README.
    // Never used in production (seeder is env-gated). OWASP A02 compliance:
    // even this dev password is hashed before storage — never persisted raw.
    private const string SeedPassword = "SeedPass@123";

    public DevelopmentDataSeeder(
        PropelIQDbContext db,
        IPasswordHasher<Staff> staffHasher,
        IPasswordHasher<Admin> adminHasher,
        IPasswordHasher<Patient> patientHasher)
    {
        _db = db;
        _staffHasher = staffHasher;
        _adminHasher = adminHasher;
        _patientHasher = patientHasher;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedAdminAsync(cancellationToken);
        await SeedStaffAsync(cancellationToken);
        await SeedPatientsAsync(cancellationToken);
        await SeedAppointmentsAsync(cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAdminAsync(CancellationToken ct)
    {
        // Idempotency guard: skip if sentinel admin already exists
        if (await _db.Admins.AnyAsync(a => a.Username == "seed-admin-1", ct))
            return;

        var admin = new Admin
        {
            Id = Guid.NewGuid(),
            Username = "seed-admin-1",
            AccessPrivileges = int.MaxValue, // all privileges for dev admin
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        admin.AuthCredentials = _adminHasher.HashPassword(admin, SeedPassword);
        _db.Admins.Add(admin);
    }

    private async Task SeedStaffAsync(CancellationToken ct)
    {
        if (await _db.Staff.AnyAsync(s => s.Username == "seed-staff-front-desk", ct))
            return;

        var roles = new[]
        {
            (Username: "seed-staff-front-desk",  Role: StaffRole.FrontDesk,        Bits: 0b_0000_0001),
            (Username: "seed-staff-call-center", Role: StaffRole.CallCenter,       Bits: 0b_0000_0011),
            (Username: "seed-staff-clinical",    Role: StaffRole.ClinicalReviewer, Bits: 0b_0000_0111)
        };

        foreach (var (username, role, bits) in roles)
        {
            var staff = new Staff
            {
                Id = Guid.NewGuid(),
                Username = username,
                Role = role,
                PermissionsBitfield = bits,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            staff.AuthCredentials = _staffHasher.HashPassword(staff, SeedPassword);
            _db.Staff.Add(staff);
        }
    }

    private async Task SeedPatientsAsync(CancellationToken ct)
    {
        if (await _db.Patients.AnyAsync(p => p.Email == "seed-patient-1@dev.local", ct))
            return;

        var alice = new Patient
        {
            Id = Guid.NewGuid(),
            Email = "seed-patient-1@dev.local",
            Name = "Alice Dev",
            Dob = new DateOnly(1985, 4, 10),
            Phone = "555-0001",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        alice.AuthCredentials = _patientHasher.HashPassword(alice, SeedPassword);

        var bob = new Patient
        {
            Id = Guid.NewGuid(),
            Email = "seed-patient-2@dev.local",
            Name = "Bob Dev",
            Dob = new DateOnly(1972, 11, 23),
            Phone = "555-0002",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        bob.AuthCredentials = _patientHasher.HashPassword(bob, SeedPassword);

        _db.Patients.AddRange(alice, bob);
    }

    private async Task SeedAppointmentsAsync(CancellationToken ct)
    {
        // Only seed booked/completed appointments when none exist yet
        if (await _db.Appointments.AnyAsync(a => a.Status != AppointmentStatus.Available, ct))
        {
            // Even when booked rows exist, always ensure Available slots are present
            // so the availability search returns results.
        }

        // Seed Available slots for the next 14 days if none exist.
        if (await _db.Appointments.AnyAsync(a => a.Status == AppointmentStatus.Available, ct))
            return;

        var providers = new[]
        {
            (Name: "Dr. Sarah Chen",      Specialty: "Family Medicine"),
            (Name: "Dr. Marcus Rivera",   Specialty: "Internal Medicine"),
            (Name: "Dr. Priya Nair",      Specialty: "General Practice"),
        };

        var visitTypes = new[] { "in-person", "telehealth" };

        var baseDate = DateTime.UtcNow.Date;

        for (int dayOffset = 1; dayOffset <= 14; dayOffset++)
        {
            var slotDate = baseDate.AddDays(dayOffset);

            // Morning block: 09:00 – 12:00 in 30-min increments
            for (int h = 9; h < 12; h++)
            {
                foreach (var (providerName, _) in providers)
                {
                    var visitType = (h % 2 == 0) ? "in-person" : "telehealth";
                    _db.Appointments.Add(new Appointment
                    {
                        Id              = Guid.NewGuid(),
                        SlotDatetime    = slotDate.AddHours(h).AddMinutes(30 * (h % 2)),
                        Status          = AppointmentStatus.Available,
                        Provider        = providerName,
                        VisitType       = visitType,
                        Location        = visitType == "telehealth" ? "Telehealth" : "PropelIQ Clinic — Suite 200",
                        DurationMinutes = 30,
                        CreatedAt       = DateTime.UtcNow,
                        UpdatedAt       = DateTime.UtcNow,
                    });
                }
            }

            // Afternoon block: 14:00 – 17:00
            for (int h = 14; h < 17; h++)
            {
                foreach (var (providerName, _) in providers)
                {
                    var visitType = (h % 2 == 0) ? "telehealth" : "in-person";
                    _db.Appointments.Add(new Appointment
                    {
                        Id              = Guid.NewGuid(),
                        SlotDatetime    = slotDate.AddHours(h),
                        Status          = AppointmentStatus.Available,
                        Provider        = providerName,
                        VisitType       = visitType,
                        Location        = visitType == "telehealth" ? "Telehealth" : "PropelIQ Clinic — Suite 200",
                        DurationMinutes = 30,
                        CreatedAt       = DateTime.UtcNow,
                        UpdatedAt       = DateTime.UtcNow,
                    });
                }
            }
        }

        // Keep the existing booked/completed appointments for seeded patients
        var patients = await _db.Patients
            .Where(p => p.Email.StartsWith("seed-patient-"))
            .ToListAsync(ct);

        if (!await _db.Appointments.AnyAsync(a => a.PatientId != null, ct))
        {
            foreach (var patient in patients)
            {
                _db.Appointments.Add(new Appointment
                {
                    Id           = Guid.NewGuid(),
                    PatientId    = patient.Id,
                    SlotDatetime = DateTime.UtcNow.AddDays(7),
                    Status       = AppointmentStatus.Booked,
                    Provider     = "Dr. Sarah Chen",
                    VisitType    = "in-person",
                    Location     = "PropelIQ Clinic — Suite 200",
                    DurationMinutes = 30,
                    CreatedAt    = DateTime.UtcNow,
                    UpdatedAt    = DateTime.UtcNow,
                });
                _db.Appointments.Add(new Appointment
                {
                    Id           = Guid.NewGuid(),
                    PatientId    = patient.Id,
                    SlotDatetime = DateTime.UtcNow.AddDays(-14),
                    Status       = AppointmentStatus.Completed,
                    Provider     = "Dr. Marcus Rivera",
                    VisitType    = "in-person",
                    Location     = "PropelIQ Clinic — Suite 200",
                    DurationMinutes = 30,
                    CreatedAt    = DateTime.UtcNow,
                    UpdatedAt    = DateTime.UtcNow,
                });
            }
        }
    }
}
