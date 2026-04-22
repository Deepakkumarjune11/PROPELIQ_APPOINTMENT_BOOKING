using PatientAccess.Domain.Enums;

namespace Admin.Application.Validators;

/// <summary>
/// Validates role × permission combinations before persisting changes.
/// Conflicts are business-logic rules (UC-006 extension 2a) rather than data constraints.
/// </summary>
public static class PermissionValidator
{
    // Bit positions — must match client-side Permissions constants in client/src/lib/permissions.ts
    public const int ViewPatientCharts  = 1 << 0; // 1
    public const int VerifyClinicalData = 1 << 1; // 2
    public const int ManageAppointments = 1 << 2; // 4
    public const int UploadDocuments    = 1 << 3; // 8
    public const int ViewMetrics        = 1 << 4; // 16

    // (role, forbiddenBit, errorMessage) — extend this table to add new conflict rules.
    private static readonly IReadOnlyList<(StaffRole Role, int ForbiddenBit, string Message)> Conflicts =
    [
        (StaffRole.FrontDesk,  VerifyClinicalData, "FrontDesk role cannot hold VerifyClinicalData permission"),
        (StaffRole.CallCenter, ViewPatientCharts,  "CallCenter role cannot hold ViewPatientCharts permission"),
    ];

    /// <summary>
    /// Returns a human-readable conflict message if the combination is invalid,
    /// or <c>null</c> when the combination is permitted.
    /// </summary>
    public static string? Validate(StaffRole role, int permissionsBitfield)
    {
        foreach (var (r, bit, msg) in Conflicts)
        {
            if (r == role && (permissionsBitfield & bit) != 0)
                return msg;
        }

        return null;
    }
}
