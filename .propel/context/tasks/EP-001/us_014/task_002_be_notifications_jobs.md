# Task - task_002_be_notifications_jobs

## Requirement Reference

- **User Story**: US_014 — Reminders, Calendar Sync & PDF Confirmation
- **Story Location**: `.propel/context/tasks/EP-001/us_014/us_014.md`
- **Acceptance Criteria**:
  - AC-1: When the booking transaction completes, the system queues background jobs to send SMS via Twilio and email via SendGrid with appointment details per FR-007.
  - AC-4: When the PDF generation job runs, a PDF appointment confirmation is generated using PDFSharp and emailed to the patient with booking details per TR-014.
- **Edge Cases**:
  - SMS delivery fails → Hangfire retries once (1 automatic retry); on final failure, logs `CommunicationLog` with `Status = Failed` for staff visibility (FR-007 extension 5a).
  - Twilio/SendGrid rate limits → Hangfire job queue ensures confirmation jobs are enqueued with high priority; reminders deferred to lower priority queue.

---

## Design References (Frontend Tasks Only)

| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

---

## Applicable Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | .NET 8 ASP.NET Core | 8.0 LTS |
| Background Jobs | Hangfire | 1.8.x |
| SMS | Twilio Programmable SMS (free tier) | latest |
| Email | SendGrid Email API (free tier) | latest |
| PDF | PDFSharp (OSS) | 6.x (.NET 6+) |
| ORM | Entity Framework Core | 8.0 |
| Database | PostgreSQL | 15.x |
| Logging | Serilog | 3.x |
| Configuration | Microsoft.Extensions.Options | .NET 8 built-in |

> All code and libraries MUST be compatible with versions above. Twilio: `Twilio` NuGet package. SendGrid: `SendGrid` NuGet package. PDFSharp: `PdfSharp` NuGet package (MIT licence).

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

Implement the **notifications and PDF confirmation pipeline** within the `PatientAccess` bounded context using Hangfire for background job processing (TR-009):

1. **Hangfire setup** — register Hangfire with PostgreSQL storage (Hangfire.PostgreSql) in `ServiceCollectionExtensions.cs`. Configure two named queues: `"critical"` (confirmations, PDF) and `"default"` (reminders). Expose the Hangfire dashboard only in development (security: not accessible in production without auth).

2. **PDF generation** — `PdfSharpConfirmationService` generates a PDF byte array containing appointment date/time, patient name, visit reason, and clinic contact details. The PDF is stored as a `byte[]` in a `CommunicationLog.PdfBytes` column (nullable) so it can be served by `GET /api/v1/appointments/{appointmentId}/pdf` without regenerating.

3. **Twilio SMS job** (`SendReminderSmsJob`) — calls Twilio REST API to send an SMS to `patient.Phone` with appointment reminder text. Decorated with `[AutomaticRetry(Attempts = 1)]` (one retry per FR-007 edge case "retries once"). Records `CommunicationLog` with channel=SMS, status=Sent|Failed.

4. **SendGrid email + PDF job** (`SendConfirmationEmailPdfJob`) — generates PDF via `IPdfGenerationService`, attaches as `application/pdf` to a SendGrid `SendEmailRequest`, sends to `patient.Email`. Decorated with `[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] {10, 30, 90})]` (exponential backoff per TR-023). Records `CommunicationLog` with channel=Email, status=Sent|Failed, stores `PdfBytes` on success.

5. **Enqueue from handler** — `RegisterForAppointmentHandler` (modified by task_003/US_013) enqueues both jobs after `SaveChangesAsync` using `IBackgroundJobClient`. Confirmations go to the `"critical"` queue; recurring reminders (24h before appointment) are scheduled with `BackgroundJob.Schedule` for `(appointment.SlotDatetime - 24h)`.

6. **`GET /api/v1/appointments/{appointmentId}/pdf` endpoint** — retrieves `CommunicationLog.PdfBytes` for the appointment and returns `File(bytes, "application/pdf", "confirmation.pdf")`. Returns `202 Accepted` with `Retry-After: 10` if the PDF job has not yet run.

**API key security (OWASP A02)**: Twilio `AccountSid`/`AuthToken` and SendGrid `ApiKey` must be read from `IConfiguration` (environment variables in production; `appsettings.Development.json` placeholders). Keys must never be hardcoded.

---

## Dependent Tasks

- **task_002_be_patient_registration_api.md** (US_010) — `RegisterForAppointmentHandler` must be in place for the job-enqueue modification. `Patient.Phone` and `Patient.Email` used in job payloads.
- **task_003_be_atomic_booking_transaction.md** (US_013) — handler modifications must not conflict; job enqueue is appended after the existing `SaveChangesAsync`.

---

## Impacted Components

| Action | Module | Description |
|--------|--------|-------------|
| CREATE | `PatientAccess.Data` | `Entities/CommunicationLog.cs` — Id, PatientId, AppointmentId, Channel (enum: SMS/Email), Status (enum: Sent/Failed), AttemptCount, PdfBytes (byte[]?), CreatedAt |
| CREATE | `PatientAccess.Data` | `Configurations/CommunicationLogConfiguration.cs` — `communication_log` table, FK to Patient + Appointment (Restrict), nullable `pdf_bytes` bytea column |
| CREATE | EF Migration | `AddCommunicationLog` — creates `communication_log` table |
| CREATE | `PatientAccess.Application` | `Services/IPdfGenerationService.cs` + `Services/PdfSharpConfirmationService.cs` — generates PDF from `AppointmentConfirmationDetails` DTO |
| CREATE | `PatientAccess.Application` | `Jobs/SendReminderSmsJob.cs` — Hangfire job: Twilio SMS, `[AutomaticRetry(Attempts=1)]`, CommunicationLog write |
| CREATE | `PatientAccess.Application` | `Jobs/SendConfirmationEmailPdfJob.cs` — Hangfire job: PDF generation + SendGrid attach + send, `[AutomaticRetry]` with backoff, CommunicationLog write + PdfBytes persist |
| MODIFY | `PatientAccess.Application` | `Commands/RegisterForAppointment/RegisterForAppointmentHandler.cs` — inject `IBackgroundJobClient`; enqueue `SendReminderSmsJob` (critical queue) + `SendConfirmationEmailPdfJob` (critical queue) + scheduled reminder (24h before slot) |
| CREATE | `PatientAccess.Presentation` | `Controllers/AppointmentsController.cs` — add `GET /{appointmentId}/pdf` action (serves `CommunicationLog.PdfBytes` or 202) |
| MODIFY | `PatientAccess.Presentation` | `ServiceCollectionExtensions.cs` — register Hangfire (PostgreSQL storage), `IPdfGenerationService`, configure Twilio/SendGrid options |
| MODIFY | `server/src/PropelIQ.Api` | `appsettings.json` + `appsettings.Development.json` — add `Twilio`, `SendGrid` config sections |

---

## Implementation Plan

1. **`CommunicationLog` entity** — `Channel` as `HasConversion<string>()` (SMS/Email); `Status` as varchar; nullable `PdfBytes` as `bytea` (`HasColumnType("bytea")`).

2. **`PdfSharpConfirmationService`** — creates a `PdfDocument`, adds a single page, draws appointment fields (date, time, patient name, visit reason, clinic name) using `XGraphics`. Returns `byte[]` via `document.Save(memoryStream)`. KISS: plain black-and-white text layout; no images.

3. **`SendReminderSmsJob`** — Hangfire job class with `Execute(Guid patientId, Guid appointmentId, string phoneNumber, string appointmentSummary)`:
   ```
   twilioClient.MessageResource.Create(
     body: $"Reminder: Your appointment is on {appointmentSummary}. Reply STOP to opt out.",
     from: new PhoneNumber(twilioFromNumber),
     to: new PhoneNumber(phoneNumber))
   Log CommunicationLog { Channel=SMS, Status=Sent|Failed, AttemptCount=1 }
   ```

4. **`SendConfirmationEmailPdfJob`** — `Execute(Guid patientId, Guid appointmentId, string email, AppointmentConfirmationDetails details)`:
   ```
   pdfBytes = _pdfService.Generate(details)
   sendGridClient.SendEmailAsync(new SendGridMessage {
     Subject = "Your appointment confirmation",
     HtmlContent = "<p>Please find your confirmation attached.</p>",
     Attachments = [{ Content = Convert.ToBase64String(pdfBytes), Type = "application/pdf", Filename = "confirmation.pdf" }]
   })
   _db.CommunicationLogs.Add(new CommunicationLog { ..., PdfBytes = pdfBytes, Status = Sent|Failed })
   await _db.SaveChangesAsync()
   ```

5. **Job enqueue in `RegisterForAppointmentHandler`** — after `SaveChangesAsync`:
   ```csharp
   _backgroundJobClient.Enqueue<SendConfirmationEmailPdfJob>(
       queue: "critical",
       job => job.Execute(patient.Id, appointment.Id, patient.Email, details));
   _backgroundJobClient.Enqueue<SendReminderSmsJob>(
       queue: "critical",
       job => job.Execute(patient.Id, appointment.Id, patient.Phone, appointmentSummary));
   // Schedule 24h-before reminder SMS (lower priority)
   _backgroundJobClient.Schedule<SendReminderSmsJob>(
       queue: "default",
       job => job.Execute(patient.Id, appointment.Id, patient.Phone, "Reminder: " + appointmentSummary),
       appointment.SlotDatetime.AddHours(-24));
   ```

6. **`GET /{appointmentId}/pdf` endpoint**:
   ```csharp
   var log = await _db.CommunicationLogs
       .Where(c => c.AppointmentId == appointmentId && c.Channel == CommunicationChannel.Email && c.PdfBytes != null)
       .OrderByDescending(c => c.CreatedAt)
       .FirstOrDefaultAsync(ct);
   if (log?.PdfBytes is null)
       return StatusCode(202);   // Job not yet run; FE retries after Retry-After header
   return File(log.PdfBytes, "application/pdf", "confirmation.pdf");
   ```

7. **Hangfire DI** in `ServiceCollectionExtensions.cs`:
   ```csharp
   services.AddHangfire(c => c.UsePostgreSqlStorage(
       configuration.GetConnectionString("DefaultConnection"),
       new PostgreSqlStorageOptions { QueuePollInterval = TimeSpan.FromSeconds(5) }));
   services.AddHangfireServer(opts => opts.Queues = new[] { "critical", "default" });
   services.AddSingleton<IPdfGenerationService, PdfSharpConfirmationService>();
   ```

---

## Current Project State

```
server/src/Modules/PatientAccess/
  PatientAccess.Data/
    Entities/
      Patient.cs, Appointment.cs, IntakeResponse.cs   ← Stable
      CommunicationLog.cs                              ← Does NOT exist yet — CREATE
    PropelIQDbContext.cs                               ← MODIFY: add DbSet<CommunicationLog>
  PatientAccess.Application/
    Commands/
      RegisterForAppointment/
        RegisterForAppointmentHandler.cs              ← MODIFY: inject IBackgroundJobClient, enqueue jobs
    Services/                                          ← ADD IPdfGenerationService + PdfSharpConfirmationService
    Jobs/                                              ← Does NOT exist yet — CREATE
  PatientAccess.Presentation/
    Controllers/
      AppointmentsController.cs                       ← MODIFY: add GET /{appointmentId}/pdf
    ServiceCollectionExtensions.cs                    ← MODIFY: Hangfire + options
server/src/PropelIQ.Api/
  appsettings.json, appsettings.Development.json      ← MODIFY: Twilio + SendGrid sections
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `PatientAccess.Data/Entities/CommunicationLog.cs` | Entity with Channel/Status enums, nullable PdfBytes (bytea) |
| CREATE | `PatientAccess.Data/Configurations/CommunicationLogConfiguration.cs` | Table `communication_log`, FK Restrict, `bytea` column type for PdfBytes |
| MODIFY | `PatientAccess.Data/PropelIQDbContext.cs` | Add `DbSet<CommunicationLog> CommunicationLogs` |
| CREATE | EF Migration | `AddCommunicationLog` |
| CREATE | `PatientAccess.Application/Services/IPdfGenerationService.cs` | Interface with `byte[] Generate(AppointmentConfirmationDetails)` |
| CREATE | `PatientAccess.Application/Services/PdfSharpConfirmationService.cs` | PDFSharp implementation generating single-page appointment PDF |
| CREATE | `PatientAccess.Application/Jobs/SendReminderSmsJob.cs` | Hangfire job — Twilio SMS, `[AutomaticRetry(Attempts=1)]`, CommunicationLog |
| CREATE | `PatientAccess.Application/Jobs/SendConfirmationEmailPdfJob.cs` | Hangfire job — PDFSharp + SendGrid, exponential backoff retry, CommunicationLog with PdfBytes |
| MODIFY | `PatientAccess.Application/Commands/RegisterForAppointment/RegisterForAppointmentHandler.cs` | Inject `IBackgroundJobClient`; enqueue confirmation + reminder jobs after `SaveChangesAsync` |
| MODIFY | `PatientAccess.Presentation/Controllers/AppointmentsController.cs` | Add `GET /{appointmentId:guid}/pdf` — serve PDF bytes or 202 |
| MODIFY | `PatientAccess.Presentation/ServiceCollectionExtensions.cs` | Add Hangfire (PostgreSqlStorage), `IPdfGenerationService` singleton, bind Twilio/SendGrid options |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `Twilio: { AccountSid, AuthToken, FromNumber }` + `SendGrid: { ApiKey, FromEmail }` (all empty — env-var overrides) |

---

## External References

- [Hangfire 1.8.x — PostgreSQL storage (Hangfire.PostgreSql)](https://www.hangfire.io/blog/2015/12/09/hangfire-1.5.1.html)
- [Hangfire — `AutomaticRetryAttribute` + named queues](https://docs.hangfire.io/en/latest/background-processing/configuring-queues.html)
- [PDFSharp 6.x — .NET 6/8 OSS PDF library](https://docs.pdfsharp.net/)
- [Twilio .NET SDK — MessageResource.Create](https://www.twilio.com/docs/sms/quickstart/csharp-dotnet-core)
- [SendGrid .NET SDK — `SendGridClient.SendEmailAsync`](https://docs.sendgrid.com/for-developers/sending-email/v3-csharp-code-example)
- [TR-009 — Hangfire for background job processing](`.propel/context/docs/design.md#TR-009`)
- [TR-023 — Exponential backoff for external API calls](`.propel/context/docs/design.md#TR-023`)
- [OWASP A02 — API keys in environment variables only](https://owasp.org/Top10/A02_2021-Cryptographic_Failures/)

---

## Build Commands

```bash
# From server/
dotnet restore
dotnet build PropelIQ.slnx

# Generate migration
dotnet ef migrations add AddCommunicationLog \
  --project src/Modules/PatientAccess/PatientAccess.Data \
  --startup-project src/PropelIQ.Api

dotnet ef database update \
  --project src/Modules/PatientAccess/PatientAccess.Data \
  --startup-project src/PropelIQ.Api
```

---

## Implementation Validation Strategy

- [ ] `SendConfirmationEmailPdfJob` executes → `CommunicationLog` row created with `Status=Sent`, `PdfBytes` non-null, `Channel=Email`
- [ ] `SendReminderSmsJob` executes → `CommunicationLog` row created with `Status=Sent`, `Channel=SMS`
- [ ] `SendReminderSmsJob` failure → Hangfire retries once; after both attempts fail, `CommunicationLog.Status = Failed`
- [ ] `GET /api/v1/appointments/{appointmentId}/pdf` returns `application/pdf` bytes when PDF job has run
- [ ] `GET /api/v1/appointments/{appointmentId}/pdf` returns `202 Accepted` when PDF job has not yet run
- [ ] `RegisterForAppointmentHandler` enqueues both jobs after `SaveChangesAsync` completes (jobs in critical queue)
- [ ] 24h-before reminder scheduled correctly (`appointment.SlotDatetime.AddHours(-24)`)
- [ ] Twilio and SendGrid API keys NOT hardcoded — empty strings in `appsettings.json`, sourced from env in production
- [ ] Hangfire dashboard accessible at `/hangfire` in development; unavailable in production without auth
- [ ] `dotnet build` passes with zero errors after migration

---

## Implementation Checklist

- [ ] Create `CommunicationLog.cs` entity (Id, PatientId, AppointmentId, Channel, Status, AttemptCount, PdfBytes?, CreatedAt)
- [ ] Create `CommunicationLogConfiguration.cs` — `communication_log` table, `bytea` for PdfBytes, FK Restrict
- [ ] Modify `PropelIQDbContext.cs` — add `DbSet<CommunicationLog>`
- [ ] Run `dotnet ef migrations add AddCommunicationLog`
- [ ] Create `IPdfGenerationService.cs` + `PdfSharpConfirmationService.cs` — single-page PDF with appointment fields
- [ ] Create `SendReminderSmsJob.cs` — `[AutomaticRetry(Attempts=1)]`, Twilio SMS, CommunicationLog write
- [ ] Create `SendConfirmationEmailPdfJob.cs` — PDF generation, SendGrid attach + send, `[AutomaticRetry]` exponential backoff, CommunicationLog + PdfBytes persist
- [ ] Modify `RegisterForAppointmentHandler.cs` — inject `IBackgroundJobClient`; enqueue confirmation email+PDF (critical) + immediate SMS (critical) + scheduled 24h reminder (default) after `SaveChangesAsync`
- [ ] Modify `AppointmentsController.cs` — add `GET /{appointmentId:guid}/pdf` action; serve bytes or 202
- [ ] Modify `ServiceCollectionExtensions.cs` — Hangfire (PostgreSQL storage, two queues), `IPdfGenerationService` singleton, bind Twilio/SendGrid options from IConfiguration
- [ ] Modify `appsettings.json` + `appsettings.Development.json` — add empty-placeholder Twilio + SendGrid config sections
