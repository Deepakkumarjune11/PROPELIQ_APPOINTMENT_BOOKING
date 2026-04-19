# Task - task_001_be_phi_encryption_infrastructure

## Requirement Reference

- **User Story**: US_027 — PHI Encryption & Data Protection
- **Story Location**: `.propel/context/tasks/EP-006/us_027/us_027.md`
- **Acceptance Criteria**:
  - AC-1: PHI columns (`Patient.Name`, `Patient.Phone`, `Patient.InsuranceProvider`, `Patient.InsuranceMemberId`, `IntakeResponse.Answers`, `ClinicalDocument.FileReference`, `ExtractedFact.FactText`, `PatientView360.ConsolidatedFacts`) are encrypted at rest using AES-256-GCM (provided by the .NET Data Protection API default algorithm) before any write to PostgreSQL per NFR-003 and DR-015. Decryption occurs transparently on read through the application layer only.
  - AC-2: Kestrel enforces TLS 1.3 as the minimum protocol via `ConfigureHttpsDefaults`; connections negotiating TLS < 1.3 are rejected per DR-015. IIS production hosting defers TLS enforcement to Windows Schannel (configured via IIS Crypto / registry — documented, no code change in `web.config`).
  - AC-3: `.NET Data Protection API` is configured with a 90-day default key lifetime (`SetDefaultKeyLifetime(TimeSpan.FromDays(90))`). On rotation the API automatically issues a new key; all new writes use the latest key; old ciphertext remains decryptable via retained older keys. Manual re-encryption of existing rows (migrating from old key to new) is handled by the `PhiReEncryptionJob` documented in this task but deferred for a future sprint (out of scope for phase-1 delivery per DR-015 clarification).
  - AC-5: After applying `PhiEncryptedConverter` on all PHI columns, any direct `psql` query of those columns returns ciphertext (Base64-encoded AES-256-GCM payload); plaintext is only accessible through the application layer with the correct Data Protection key.
- **Edge Cases**:
  - `Patient.Email` is deliberately **not** encrypted — it is the primary login identifier used in `WHERE email = @email` lookups and has a unique index (`uix_patient_email`). Encrypting it would break authentication and equality queries. Email is a system identifier, not classified as demographics PHI under DR-015.
  - `Patient.Dob` (`DateOnly`) is not encrypted in this task — the `PhiEncryptedConverter` is a `ValueConverter<string, string>`; encrypting date/numeric columns requires a separate `ValueConverter<DateOnly, string>` with a column-type change to `text`. This is noted as a follow-on item.
  - `PatientView360.ConflictFlags` (`string[]` / `text[]`) is not listed in DR-015's PHI column inventory — it stores internal metadata (flag names) rather than clinical text values. Not encrypted.
  - `IntakeResponse.Answers` and `PatientView360.ConsolidatedFacts` currently use `HasColumnType("jsonb")`. After applying the converter, ciphertext is stored as plain text — the column type must be changed to `text` in the migration (task_002); these configurations must be updated here to reflect `HasColumnType("text")`.
  - If `IDataProtector.Unprotect` fails (tampered ciphertext or key not found) → `CryptographicException` is thrown. Application layer should **not** swallow this — let it surface as 500 to prevent silent data corruption.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | .NET | 8 LTS |
| API Framework | ASP.NET Core Web API | 8.0 |
| ORM | Entity Framework Core | 8.0 |
| Security | .NET Data Protection API | 8.0 |
| Database | PostgreSQL | 15.x |
| Language | C# | 12 |

> **Tech decision (design.md line 134)**: `.NET Data Protection API 8.0` is the mandated encryption library per NFR-003 and DR-015. It uses AES-256-CBC with HMACSHA256 (or AES-256-GCM depending on platform) internally. No third-party encryption library is permitted (NFR-015 OSS constraint).

---

## AI References (AI Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

---

## Mobile References (Mobile Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

---

## Task Overview

Implement column-level PHI encryption using the `.NET Data Protection API` as an EF Core `ValueConverter`. The converter is applied to the eight designated PHI columns across five entities. Also configures Kestrel TLS 1.3 enforcement and 90-day key rotation.

### PHI Column Inventory (DR-015)

| Entity | Property | Current Column Type | New Column Type | Converter Applied |
|--------|----------|--------------------|-----------------|--------------------|
| `Patient` | `Name` | varchar(200) | text | `PhiEncryptedConverter` |
| `Patient` | `Phone` | varchar(30) | text | `PhiEncryptedConverter` |
| `Patient` | `InsuranceProvider` | varchar(200) nullable | text nullable | `PhiEncryptedNullableConverter` |
| `Patient` | `InsuranceMemberId` | varchar(100) nullable | text nullable | `PhiEncryptedNullableConverter` |
| `IntakeResponse` | `Answers` | jsonb → text | text | `PhiEncryptedConverter` |
| `ClinicalDocument` | `FileReference` | varchar(2048) | text | `PhiEncryptedConverter` |
| `ExtractedFact` | `FactText` | varchar(2000) | text | `PhiEncryptedConverter` |
| `PatientView360` | `ConsolidatedFacts` | jsonb → text | text | `PhiEncryptedConverter` |

---

## Dependent Tasks

- **task_002_db_phi_column_migration.md** (US_027) — must run after this task; the migration widens/retypes the eight PHI columns to accommodate variable-length ciphertext.

---

## Impacted Components

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/src/Modules/PatientAccess/PatientAccess.Data/Converters/PhiEncryptedConverter.cs` | `ValueConverter<string, string>` wrapping `IDataProtector.Protect`/`Unprotect`; `PhiEncryptedNullableConverter` variant for nullable string columns |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/Configurations/PatientConfiguration.cs` | Apply `PhiEncryptedConverter` to `Name`, `Phone`; `PhiEncryptedNullableConverter` to `InsuranceProvider`, `InsuranceMemberId`; remove `HasMaxLength` from all four PHI columns |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/Configurations/IntakeResponseConfiguration.cs` | Apply `PhiEncryptedConverter` to `Answers`; change `HasColumnType("jsonb")` → `HasColumnType("text")` |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/Configurations/ClinicalDocumentConfiguration.cs` | Apply `PhiEncryptedConverter` to `FileReference`; remove `HasMaxLength(2048)` |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/Configurations/ExtractedFactConfiguration.cs` | Apply `PhiEncryptedConverter` to `FactText`; remove `HasMaxLength(2000)` |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/Configurations/PatientView360Configuration.cs` | Apply `PhiEncryptedConverter` to `ConsolidatedFacts`; change `HasColumnType("jsonb")` → `HasColumnType("text")` |
| MODIFY | `server/src/Modules/PatientAccess/PatientAccess.Data/PropelIQDbContext.cs` | Accept `IDataProtectionProvider` constructor injection; create `IDataProtector` for `"phi-data-at-rest"` purpose; pass converter instances to `OnModelCreating` |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | Add `AddDataProtection()` with `PersistKeysToFileSystem` + `SetDefaultKeyLifetime(90 days)`; add Kestrel `ConfigureHttpsDefaults` enforcing `SslProtocols.Tls13` |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"DataProtection": { "KeysPath": "keys/phi" }` section |
| MODIFY | `server/src/PropelIQ.Api/ConfigurationValidator.cs` | Add `"DataProtection:KeysPath"` to `RequiredKeys` array |

---

## Implementation Plan

### Part A — `PhiEncryptedConverter` and `PhiEncryptedNullableConverter`

```csharp
namespace PatientAccess.Data.Converters;

/// <summary>
/// EF Core ValueConverter that transparently encrypts string PHI columns using
/// .NET Data Protection API (AES-256-GCM) before persisting and decrypts on read.
/// Satisfies DR-015 column-level encryption and NFR-003 AES-256 at rest.
/// </summary>
public sealed class PhiEncryptedConverter : ValueConverter<string, string>
{
    public PhiEncryptedConverter(IDataProtector protector)
        : base(
            plaintext => protector.Protect(plaintext),
            ciphertext => protector.Unprotect(ciphertext))
    { }
}

/// <summary>
/// Nullable variant — passes null through without calling Protect/Unprotect.
/// </summary>
public sealed class PhiEncryptedNullableConverter : ValueConverter<string?, string?>
{
    public PhiEncryptedNullableConverter(IDataProtector protector)
        : base(
            plaintext => plaintext == null ? null : protector.Protect(plaintext),
            ciphertext => ciphertext == null ? null : protector.Unprotect(ciphertext))
    { }
}
```

### Part B — `PropelIQDbContext` — inject `IDataProtectionProvider`

Add constructor overload that accepts `IDataProtectionProvider` and instantiates the converters under the purpose string `"phi-data-at-rest"`. The second constructor (with only `DbContextOptions`) is retained for EF tooling (migrations/snapshot generation) which does not inject `IDataProtectionProvider`:

```csharp
private readonly PhiEncryptedConverter? _phiConverter;
private readonly PhiEncryptedNullableConverter? _phiNullableConverter;

// Full runtime constructor — receives IDataProtectionProvider from DI
public PropelIQDbContext(
    DbContextOptions<PropelIQDbContext> options,
    IDataProtectionProvider dataProtection)
    : base(options)
{
    var protector = dataProtection.CreateProtector("phi-data-at-rest");
    _phiConverter = new PhiEncryptedConverter(protector);
    _phiNullableConverter = new PhiEncryptedNullableConverter(protector);
}

// EF tooling constructor (used by dotnet ef migrations add / update)
// Converters are null — migrations do not process PHI values
public PropelIQDbContext(DbContextOptions<PropelIQDbContext> options)
    : base(options)
{ }
```

Pass `_phiConverter` and `_phiNullableConverter` to each affected `IEntityTypeConfiguration` by injecting them at `ApplyConfiguration` call sites, or use a factory pattern:

```csharp
// In OnModelCreating — pass converters to configurations that need them
if (_phiConverter is not null)
{
    modelBuilder.ApplyConfiguration(new PatientConfiguration(_phiConverter, _phiNullableConverter!));
    modelBuilder.ApplyConfiguration(new IntakeResponseConfiguration(_phiConverter));
    modelBuilder.ApplyConfiguration(new ClinicalDocumentConfiguration(_phiConverter));
    modelBuilder.ApplyConfiguration(new ExtractedFactConfiguration(_phiConverter));
    modelBuilder.ApplyConfiguration(new PatientView360Configuration(_phiConverter));
}
else
{
    // EF tooling path — no encryption (migrations only inspect schema, not values)
    modelBuilder.ApplyConfiguration(new PatientConfiguration());
    modelBuilder.ApplyConfiguration(new IntakeResponseConfiguration());
    modelBuilder.ApplyConfiguration(new ClinicalDocumentConfiguration());
    modelBuilder.ApplyConfiguration(new ExtractedFactConfiguration());
    modelBuilder.ApplyConfiguration(new PatientView360Configuration());
}
// Configurations that don't need PHI encryption are applied unconditionally:
modelBuilder.ApplyConfiguration(new AppointmentConfiguration());
modelBuilder.ApplyConfiguration(new IntakeResponseConfiguration()); // already in branch
// ... other non-PHI configs unchanged
```

> **Alternative**: Use a `PhiConverterFactory` passed as a single constructor arg to each configuration instead of multiple args.

### Part C — Update affected `IEntityTypeConfiguration` classes

**`PatientConfiguration`** — add constructor accepting converters:
```csharp
internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    private readonly PhiEncryptedConverter? _enc;
    private readonly PhiEncryptedNullableConverter? _encNullable;

    public PatientConfiguration() { }
    public PatientConfiguration(PhiEncryptedConverter enc, PhiEncryptedNullableConverter encNullable)
        => (_enc, _encNullable) = (enc, encNullable);

    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        // ... existing configuration unchanged ...

        builder.Property(p => p.Name)
            .IsRequired()
            .HasColumnType("text");                         // widened — no MaxLength on encrypted text
        if (_enc is not null)
            builder.Property(p => p.Name).HasConversion(_enc);

        builder.Property(p => p.Phone)
            .IsRequired()
            .HasColumnType("text");
        if (_enc is not null)
            builder.Property(p => p.Phone).HasConversion(_enc);

        builder.Property(p => p.InsuranceProvider).HasColumnType("text");
        if (_encNullable is not null)
            builder.Property(p => p.InsuranceProvider).HasConversion(_encNullable);

        builder.Property(p => p.InsuranceMemberId).HasColumnType("text");
        if (_encNullable is not null)
            builder.Property(p => p.InsuranceMemberId).HasConversion(_encNullable);
    }
}
```

Apply the same constructor pattern to `IntakeResponseConfiguration`, `ClinicalDocumentConfiguration`, `ExtractedFactConfiguration`, and `PatientView360Configuration`.

**Key changes per configuration:**
- `IntakeResponseConfiguration.Answers`: remove `HasColumnType("jsonb")` → `HasColumnType("text")` + `HasConversion(_enc)`
- `ClinicalDocumentConfiguration.FileReference`: remove `HasMaxLength(2048)` → `HasColumnType("text")` + `HasConversion(_enc)`
- `ExtractedFactConfiguration.FactText`: remove `HasMaxLength(2000)` → `HasColumnType("text")` + `HasConversion(_enc)`
- `PatientView360Configuration.ConsolidatedFacts`: remove `HasColumnType("jsonb")` → `HasColumnType("text")` + `HasConversion(_enc)`

### Part D — Register Data Protection in `Program.cs`

```csharp
// .NET Data Protection API — AES-256 PHI column encryption per DR-015 / TR-022
// Keys stored on the file system; rotate every 90 days per AC-3.
var dpKeysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(AppContext.BaseDirectory, "keys", "phi");
Directory.CreateDirectory(dpKeysPath);

builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath))
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90))
    .SetApplicationName("propeliq-phi");    // isolates keys per application; prevents cross-app decryption
```

> **Production note (AC-3)**: For Azure/IIS production, replace `PersistKeysToFileSystem` with `PersistKeysToAzureBlobStorage` + `ProtectKeysWithAzureKeyVault` for HSM-backed key storage per HIPAA HITECH §164.312(a)(2)(iv). Document in README.

### Part E — Enforce TLS 1.3 in Kestrel (`Program.cs` addition)

```csharp
// TLS 1.3 enforcement — DR-015 / AC-2
// Kestrel only: IIS production uses Windows Schannel; configure TLS 1.3 there
// using IIS Crypto tool or registry: HKLM\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.3\Server\Enabled = 1
// and disable TLS 1.0/1.1/1.2 Server keys accordingly.
builder.WebHost.ConfigureKestrel(kestrelOptions =>
{
    kestrelOptions.ConfigureHttpsDefaults(httpsOptions =>
    {
        httpsOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls13;
    });
});
```

### Part F — `appsettings.json` and `ConfigurationValidator.cs`

**`appsettings.json`** — add section:
```json
"DataProtection": {
  "KeysPath": "keys/phi"
}
```

**`ConfigurationValidator.cs`** — add to `RequiredKeys`:
```csharp
"DataProtection:KeysPath",
```

---

## Current Project State

```
server/src/
  Modules/
    PatientAccess/
      PatientAccess.Data/
        Converters/                           ← THIS TASK (create directory + 1 file)
          PhiEncryptedConverter.cs            ← THIS TASK (create)
        Configurations/
          PatientConfiguration.cs             ← MODIFY — add PHI converters, widen columns
          IntakeResponseConfiguration.cs      ← MODIFY — jsonb → text, add converter
          ClinicalDocumentConfiguration.cs    ← MODIFY — remove MaxLength, add converter
          ExtractedFactConfiguration.cs       ← MODIFY — remove MaxLength, add converter
          PatientView360Configuration.cs      ← MODIFY — jsonb → text, add converter
        PropelIQDbContext.cs                  ← MODIFY — inject IDataProtectionProvider
  PropelIQ.Api/
    Program.cs                                ← MODIFY — AddDataProtection + Kestrel TLS 1.3
    appsettings.json                          ← MODIFY — DataProtection section
    ConfigurationValidator.cs                 ← MODIFY — add DataProtection:KeysPath required key
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `server/.../PatientAccess.Data/Converters/PhiEncryptedConverter.cs` | `ValueConverter<string, string>` + nullable variant wrapping Data Protection API |
| MODIFY | `server/.../PatientAccess.Data/PropelIQDbContext.cs` | `IDataProtectionProvider` constructor injection; converter instantiation; dual constructor pattern for EF tooling compat |
| MODIFY | `server/.../PatientAccess.Data/Configurations/PatientConfiguration.cs` | Apply converters to Name, Phone, InsuranceProvider, InsuranceMemberId; remove MaxLength; `HasColumnType("text")` |
| MODIFY | `server/.../PatientAccess.Data/Configurations/IntakeResponseConfiguration.cs` | `HasColumnType("jsonb")` → `HasColumnType("text")` + `HasConversion(_enc)` on Answers |
| MODIFY | `server/.../PatientAccess.Data/Configurations/ClinicalDocumentConfiguration.cs` | Remove `HasMaxLength(2048)` + `HasColumnType("text")` + `HasConversion(_enc)` on FileReference |
| MODIFY | `server/.../PatientAccess.Data/Configurations/ExtractedFactConfiguration.cs` | Remove `HasMaxLength(2000)` + `HasColumnType("text")` + `HasConversion(_enc)` on FactText |
| MODIFY | `server/.../PatientAccess.Data/Configurations/PatientView360Configuration.cs` | `HasColumnType("jsonb")` → `HasColumnType("text")` + `HasConversion(_enc)` on ConsolidatedFacts |
| MODIFY | `server/src/PropelIQ.Api/Program.cs` | `AddDataProtection()` with filesystem persistence + 90-day key lifetime; Kestrel TLS 1.3 |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `"DataProtection": { "KeysPath": "keys/phi" }` |
| MODIFY | `server/src/PropelIQ.Api/ConfigurationValidator.cs` | Add `"DataProtection:KeysPath"` to required keys array |

---

## External References

- [.NET Data Protection API — getting started](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/introduction)
- [.NET Data Protection API — PersistKeysToFileSystem](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers#file-system)
- [.NET Data Protection API — key lifetime / rotation](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview#setdefaultkeylifetime)
- [EF Core — ValueConverter](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions)
- [Kestrel HTTPS configuration — SslProtocols](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints#configurehttpsdefaults)
- [OWASP A02 — Cryptographic Failures (AES-256 for PHI at rest)](https://owasp.org/Top10/A02_2021-Cryptographic_Failures/)
- [DR-015 — PHI column encryption list: Patient.demographics, IntakeResponse.answers, ClinicalDocument.file, ExtractedFact.value, PatientView360.consolidated_facts](../.propel/context/docs/design.md)
- [NFR-003 — AES-256 at rest, TLS 1.2+ in transit](../.propel/context/docs/design.md)
- [TR-022 — .NET Data Protection API for PHI column-level encryption](../.propel/context/docs/design.md)

---

## Build Commands

```bash
cd server
dotnet restore PropelIQ.slnx
dotnet build PropelIQ.slnx --configuration Debug
```

---

## Implementation Validation Strategy

- [ ] Unit test: `PhiEncryptedConverter` — round-trip: `Protect(plaintext)` produces ciphertext ≠ plaintext; `Unprotect(ciphertext)` returns original plaintext
- [ ] Unit test: `PhiEncryptedNullableConverter` — `null` input passes through without calling `Protect`/`Unprotect`
- [ ] Unit test: Tampered ciphertext passed to `Unprotect` → `CryptographicException` is not swallowed (bubbles up to 500)
- [ ] Integration test: Write a `Patient` record → query raw column value from `psql` → `Name` column value is ciphertext (not "John Smith" etc.)
- [ ] Integration test: Application reads same `Patient` record through EF → `patient.Name` is correctly decrypted plaintext
- [ ] Integration test: `IntakeResponse.Answers` — confirm column type is now `text` (not `jsonb`) and stored value is ciphertext
- [ ] Security test: Verify `DataProtection:KeysPath` files are outside the `wwwroot` / `publish` folder — not web-accessible
- [ ] TLS test: Start Kestrel HTTPS profile → connect using TLS 1.2 → connection refused; connect using TLS 1.3 → accepted

---

## Implementation Checklist

- [ ] Create `PhiEncryptedConverter` and `PhiEncryptedNullableConverter` in `PatientAccess.Data/Converters/`; use `IDataProtector.Protect` / `Unprotect` in lambda expressions; do not swallow `CryptographicException`
- [ ] Modify `PropelIQDbContext.cs`: add second constructor accepting `IDataProtectionProvider`; create `IDataProtector` with purpose `"phi-data-at-rest"`; use dual `if (_phiConverter is not null)` pattern in `OnModelCreating` to keep EF tooling working without encryption
- [ ] Modify five entity configurations: add optional `PhiEncryptedConverter` constructor parameter; add `HasColumnType("text")` + `HasConversion(_enc)` to all DR-015 PHI columns; remove `HasMaxLength` and `HasColumnType("jsonb")` from widened columns
- [ ] Modify `Program.cs`: add `AddDataProtection().PersistKeysToFileSystem().SetDefaultKeyLifetime(90 days).SetApplicationName("propeliq-phi")` **before** `AddDbContext`; add Kestrel `ConfigureHttpsDefaults` enforcing `SslProtocols.Tls13`
- [ ] Modify `appsettings.json`: add `"DataProtection": { "KeysPath": "keys/phi" }` section; ensure `keys/phi` is in `.gitignore` (never commit key material)
- [ ] Modify `ConfigurationValidator.cs`: add `"DataProtection:KeysPath"` to the `RequiredKeys` array
- [ ] Security check: verify `keys/phi` directory is excluded from `web.config` static file handling and is not present in `publish/` output folder; add to `.gitignore` if absent
- [ ] Add IIS Schannel TLS 1.3 enforcement instructions as a comment block in `web.config` referencing the registry path and IIS Crypto tool for production ops team
