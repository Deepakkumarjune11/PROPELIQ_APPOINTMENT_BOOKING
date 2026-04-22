using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PatientAccess.Data.Converters;

/// <summary>
/// EF Core ValueConverter that transparently encrypts <c>string</c> PHI columns using
/// .NET Data Protection API (AES-256-GCM) before persisting and decrypts on read.
/// Satisfies DR-015 column-level encryption and NFR-003 AES-256 at rest (OWASP A02).
/// </summary>
/// <remarks>
/// <c>CryptographicException</c> from <see cref="IDataProtector.Unprotect"/> is intentionally
/// NOT swallowed — tampered ciphertext or missing key surfaces as HTTP 500 to prevent
/// silent data corruption.
/// </remarks>
public sealed class PhiEncryptedConverter : ValueConverter<string, string>
{
    public PhiEncryptedConverter(IDataProtector protector)
        : base(
            plaintext  => protector.Protect(plaintext),
            ciphertext => protector.Unprotect(ciphertext))
    { }
}

/// <summary>
/// Nullable variant of <see cref="PhiEncryptedConverter"/>.
/// Passes <c>null</c> through without invoking Protect/Unprotect, preserving SQL NULL
/// semantics for optional PHI columns (e.g., <c>InsuranceProvider</c>, <c>InsuranceMemberId</c>).
/// </summary>
public sealed class PhiEncryptedNullableConverter : ValueConverter<string?, string?>
{
    public PhiEncryptedNullableConverter(IDataProtector protector)
        : base(
            plaintext  => plaintext  == null ? null : protector.Protect(plaintext),
            ciphertext => ciphertext == null ? null : protector.Unprotect(ciphertext))
    { }
}
