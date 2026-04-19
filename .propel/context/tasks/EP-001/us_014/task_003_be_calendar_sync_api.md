# Task - task_003_be_calendar_sync_api

## Requirement Reference

- **User Story**: US_014 — Reminders, Calendar Sync & PDF Confirmation
- **Story Location**: `.propel/context/tasks/EP-001/us_014/us_014.md`
- **Acceptance Criteria**:
  - AC-2: "Add to Google Calendar" and "Add to Outlook Calendar" buttons are functional via OAuth 2.0 APIs per TR-012.
  - AC-3: Clicking a calendar button creates a calendar event with appointment date, time, location, and visit reason in the selected provider per FR-007.
- **Edge Cases**:
  - Calendar sync fails (OAuth error, API quota exceeded, network error) → appointment is preserved; failure logged via Serilog; BE returns error response; FE shows "Try again" toast. No retry by Hangfire — calendar sync is patient-initiated (not critical path).

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
| Backend | .NET 8 ASP.NET Core Web API | 8.0 LTS |
| Google Calendar | Google.Apis.Calendar.v3 (.NET SDK) | latest stable |
| Outlook Calendar | Microsoft.Graph (.NET SDK) | 5.x |
| OAuth | ASP.NET Core OAuth middleware / HttpClient | .NET 8 built-in |
| Logging | Serilog | 3.x |
| API Docs | Swagger / OpenAPI | 6.x |
| Configuration | Microsoft.Extensions.Options | .NET 8 built-in |

> All code and libraries MUST be compatible with versions above.

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

Implement the **Calendar Sync API** within the `PatientAccess` bounded context — providing OAuth 2.0 authorization code flow endpoints for both Google Calendar and Microsoft Outlook Calendar per TR-012.

**OAuth flow pattern** (both providers, simplified for patient-initiated sync):

```
1. FE calls GET /api/v1/calendar/{provider}/init?appointmentId={id}
   → BE builds OAuth authorization URL (client_id, scope, redirect_uri, state=appointmentId)
   → BE returns { authUrl }

2. FE redirects browser to authUrl (window.location.href = authUrl)
   → Patient grants calendar permission on provider consent screen

3. Provider redirects to GET /api/v1/calendar/{provider}/callback?code=...&state={appointmentId}
   → BE exchanges code for access_token
   → BE calls provider API to create calendar event
   → BE redirects browser to /appointments/confirmation?calendarSynced={provider}
   → On error: redirects to /appointments/confirmation?calendarError=true
```

**Security (OWASP A10 SSRF, A01 Access Control)**:
- `state` parameter carries `appointmentId` only — not a redirect URL (prevents open redirect).
- The `redirect_uri` is a fixed value from `appsettings.json` — never taken from request parameters.
- OAuth client secrets stored in `IConfiguration` (env vars); never logged.

**Calendar event fields** sourced from `Appointment` entity:
- Title: "Medical Appointment — PropelIQ"
- Start/End: `SlotDatetime` to `SlotDatetime + 1 hour` (UTC)
- Description: visit reason (from `IntakeResponse.Answers["reasonForVisit"]` if available, else "Medical appointment")
- Location: clinic name (from `CalendarOptions.ClinicLocation` config)

---

## Dependent Tasks

- **task_001_fe_booking_confirmation_ui.md** (US_014) — FE calls `GET /api/v1/calendar/{provider}/init` and redirects to `authUrl`. Callback redirects back to `/appointments/confirmation`.
- **task_002_be_patient_registration_api.md** (US_010) — `Appointment` entity with `SlotDatetime` and `PatientId` must be accessible.

---

## Impacted Components

| Action | Module | Description |
|--------|--------|-------------|
| CREATE | `PatientAccess.Application` | `Services/ICalendarService.cs` — interface with `GetAuthorizationUrl` + `CreateEventAsync` |
| CREATE | `PatientAccess.Application` | `Services/GoogleCalendarService.cs` — Google Calendar API v3 OAuth + event creation |
| CREATE | `PatientAccess.Application` | `Services/OutlookCalendarService.cs` — Microsoft Graph Calendar OAuth + event creation |
| CREATE | `PatientAccess.Application` | `Configuration/CalendarOptions.cs` — Google/Outlook client IDs, secrets, redirect URIs, clinic location |
| CREATE | `PatientAccess.Presentation` | `Controllers/CalendarController.cs` — `GET /api/v1/calendar/{provider}/init` + `GET /api/v1/calendar/{provider}/callback` |
| MODIFY | `PatientAccess.Presentation` | `ServiceCollectionExtensions.cs` — register calendar services + bind `CalendarOptions` |
| MODIFY | `server/src/PropelIQ.Api` | `appsettings.json` + `appsettings.Development.json` — add `GoogleCalendar` + `OutlookCalendar` sections |

---

## Implementation Plan

1. **`CalendarOptions`** (`PatientAccess.Application/Configuration/CalendarOptions.cs`):
   ```csharp
   public sealed class CalendarOptions
   {
       public const string SectionName = "Calendar";
       public GoogleCalendarConfig Google { get; init; } = new();
       public OutlookCalendarConfig Outlook { get; init; } = new();
       public string ClinicLocation { get; init; } = "PropelIQ Health Clinic";
   }

   public sealed class GoogleCalendarConfig
   {
       public string ClientId { get; init; } = "";
       public string ClientSecret { get; init; } = "";
       public string RedirectUri { get; init; } = "";   // e.g. https://api.propeliq.com/api/v1/calendar/google/callback
   }

   public sealed class OutlookCalendarConfig
   {
       public string ClientId { get; init; } = "";
       public string ClientSecret { get; init; } = "";
       public string TenantId { get; init; } = "common";
       public string RedirectUri { get; init; } = "";
   }
   ```

2. **`ICalendarService`**:
   ```csharp
   public interface ICalendarService
   {
       string GetAuthorizationUrl(Guid appointmentId);
       Task CreateEventAsync(string authorizationCode, Guid appointmentId, CancellationToken ct = default);
   }
   ```

3. **`GoogleCalendarService`** — inject `IOptions<CalendarOptions>`, `PropelIQDbContext`, `ILogger<GoogleCalendarService>`:
   ```
   GetAuthorizationUrl(appointmentId):
     scope = "https://www.googleapis.com/auth/calendar.events"
     return $"https://accounts.google.com/o/oauth2/v2/auth?response_type=code&client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={Uri.EscapeDataString(scope)}&state={appointmentId}&access_type=offline"

   CreateEventAsync(code, appointmentId, ct):
     // 1. Exchange code for access_token via POST https://oauth2.googleapis.com/token
     tokenResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/token",
         FormUrlEncodedContent { code, client_id, client_secret, redirect_uri, grant_type="authorization_code" })
     accessToken = tokenResponse.access_token

     // 2. Load appointment from DB
     appointment = await _db.Appointments.Include(a => a.Patient).FirstAsync(a => a.Id == appointmentId, ct)

     // 3. Create calendar event via Google Calendar API v3
     eventBody = { summary, start: { dateTime, timeZone }, end: { dateTime, timeZone }, description, location }
     httpClient.DefaultRequestHeaders.Authorization = Bearer accessToken
     await httpClient.PostAsync(
       $"https://www.googleapis.com/calendar/v3/calendars/primary/events",
       JsonContent.Create(eventBody))
   ```

4. **`OutlookCalendarService`** — same pattern using Microsoft Identity Platform v2 + Microsoft Graph:
   ```
   GetAuthorizationUrl(appointmentId):
     scope = "https://graph.microsoft.com/Calendars.ReadWrite"
     return $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize?response_type=code&client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={Uri.EscapeDataString(scope)}&state={appointmentId}"

   CreateEventAsync(code, appointmentId, ct):
     // 1. Exchange code for access_token via POST .../token
     // 2. Load appointment from DB
     // 3. POST https://graph.microsoft.com/v1.0/me/events with Bearer token
   ```

5. **`CalendarController`** (`PatientAccess.Presentation/Controllers/CalendarController.cs`):
   ```csharp
   [Route("api/v1/calendar")]
   [ApiController]
   public class CalendarController : ControllerBase
   {
       // GET /api/v1/calendar/google/init?appointmentId={id}
       [HttpGet("{provider}/init")]
       [Authorize]
       public IActionResult InitSync(string provider, [FromQuery] Guid appointmentId)
       {
           ICalendarService service = ResolveProvider(provider);
           var authUrl = service.GetAuthorizationUrl(appointmentId);
           return Ok(new { authUrl });
       }

       // GET /api/v1/calendar/google/callback?code=...&state={appointmentId}
       // No [Authorize] — OAuth provider redirects here without auth header
       [HttpGet("{provider}/callback")]
       public async Task<IActionResult> Callback(string provider, [FromQuery] string code, [FromQuery] Guid state, CancellationToken ct)
       {
           try {
               ICalendarService service = ResolveProvider(provider);
               await service.CreateEventAsync(code, state, ct);
               return Redirect($"/appointments/confirmation?calendarSynced={provider}");
           } catch (Exception ex) {
               _logger.LogError(ex, "Calendar sync failed for provider {Provider} AppointmentId {AppointmentId}", provider, state);
               return Redirect("/appointments/confirmation?calendarError=true");
           }
       }
   }
   ```
   **Security note on Redirect**: The redirect targets are hardcoded paths (`/appointments/confirmation?...`) — not derived from request parameters. This prevents open redirect vulnerabilities (OWASP A01).

6. **DI registration** (`ServiceCollectionExtensions.cs`):
   ```csharp
   services.Configure<CalendarOptions>(configuration.GetSection(CalendarOptions.SectionName));
   services.AddHttpClient<GoogleCalendarService>();
   services.AddHttpClient<OutlookCalendarService>();
   services.AddKeyedScoped<ICalendarService, GoogleCalendarService>("google");
   services.AddKeyedScoped<ICalendarService, OutlookCalendarService>("outlook");
   ```
   `CalendarController` uses `[FromKeyedServices]` to resolve the correct service by provider key.

7. **`appsettings.json`** additions:
   ```json
   "Calendar": {
     "Google": { "ClientId": "", "ClientSecret": "", "RedirectUri": "" },
     "Outlook": { "ClientId": "", "ClientSecret": "", "TenantId": "common", "RedirectUri": "" },
     "ClinicLocation": "PropelIQ Health Clinic"
   }
   ```
   All OAuth credentials empty in `appsettings.json`; sourced from environment variables in production.

---

## Current Project State

```
server/src/Modules/PatientAccess/
  PatientAccess.Application/
    Configuration/                          ← NoShowRiskOptions exists (us_013/task_002)
    Services/                               ← Several services; ICalendarService does NOT exist
  PatientAccess.Presentation/
    Controllers/
      AppointmentsController.cs             ← Exists; CalendarController is NEW
      PatientsController.cs                 ← Exists
    ServiceCollectionExtensions.cs          ← MODIFY: calendar services + options

server/src/PropelIQ.Api/
  appsettings.json                          ← MODIFY: add Calendar section
  appsettings.Development.json              ← MODIFY: add Calendar placeholder section
```

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| CREATE | `PatientAccess.Application/Configuration/CalendarOptions.cs` | Google + Outlook OAuth client config + ClinicLocation |
| CREATE | `PatientAccess.Application/Services/ICalendarService.cs` | `GetAuthorizationUrl(appointmentId)` + `CreateEventAsync(code, appointmentId, ct)` |
| CREATE | `PatientAccess.Application/Services/GoogleCalendarService.cs` | OAuth code exchange via HttpClient + Google Calendar API v3 event creation |
| CREATE | `PatientAccess.Application/Services/OutlookCalendarService.cs` | OAuth code exchange via HttpClient + Microsoft Graph event creation |
| CREATE | `PatientAccess.Presentation/Controllers/CalendarController.cs` | `GET /{provider}/init` (Authorize, returns authUrl) + `GET /{provider}/callback` (no auth, exchanges code, creates event, redirects) |
| MODIFY | `PatientAccess.Presentation/ServiceCollectionExtensions.cs` | Bind `CalendarOptions`; `AddHttpClient` for both services; keyed DI registration |
| MODIFY | `server/src/PropelIQ.Api/appsettings.json` | Add `Calendar` section with empty placeholder credentials |
| MODIFY | `server/src/PropelIQ.Api/appsettings.Development.json` | Add `Calendar` placeholder section for dev |

---

## External References

- [Google Calendar API v3 — Create events (events.insert)](https://developers.google.com/calendar/api/v3/reference/events/insert)
- [Google OAuth 2.0 — Authorization Code Flow](https://developers.google.com/identity/protocols/oauth2/web-server)
- [Microsoft Graph — Create event (POST /me/events)](https://learn.microsoft.com/en-us/graph/api/user-post-events)
- [Microsoft Identity Platform — OAuth 2.0 authorization code flow](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-auth-code-flow)
- [ASP.NET Core 8 — Keyed services (`[FromKeyedServices]`)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services)
- [TR-012 — Google + Outlook OAuth 2.0 for calendar sync](`.propel/context/docs/design.md#TR-012`)
- [OWASP A01 — Open redirect prevention (hardcoded redirect targets)](https://owasp.org/Top10/A01_2021-Broken_Access_Control/)
- [OWASP A02 — OAuth client secrets in env vars only](https://owasp.org/Top10/A02_2021-Cryptographic_Failures/)

---

## Build Commands

```bash
# From server/
dotnet restore
dotnet build PropelIQ.slnx

# No DB migration required — calendar sync is stateless (no new entity)
```

---

## Implementation Validation Strategy

- [ ] `GET /api/v1/calendar/google/init?appointmentId={id}` returns `{ authUrl }` containing `accounts.google.com` and `state={appointmentId}`
- [ ] `GET /api/v1/calendar/outlook/init?appointmentId={id}` returns `{ authUrl }` containing `login.microsoftonline.com` and `state={appointmentId}`
- [ ] OAuth `redirect_uri` in `authUrl` matches value from `CalendarOptions` — never derived from request parameters
- [ ] Callback success: `GET /api/v1/calendar/google/callback?code=X&state={appointmentId}` → redirects to `/appointments/confirmation?calendarSynced=google`
- [ ] Callback failure (mock exception in `CreateEventAsync`): redirects to `/appointments/confirmation?calendarError=true`
- [ ] Callback does NOT expose OAuth `code` or `access_token` in redirect URL or response body
- [ ] Serilog error log emitted on `CreateEventAsync` exception; no OAuth secrets in log message
- [ ] `GET /init` returns `401` without valid auth token
- [ ] Google + Outlook OAuth credentials empty in `appsettings.json` (sourced from env vars)
- [ ] `dotnet build` passes with zero errors

---

## Implementation Checklist

- [ ] Create `CalendarOptions.cs` — `GoogleCalendarConfig` + `OutlookCalendarConfig` + `ClinicLocation`; `SectionName = "Calendar"`
- [ ] Create `ICalendarService.cs` — `GetAuthorizationUrl(Guid appointmentId)` + `CreateEventAsync(string code, Guid appointmentId, CancellationToken ct)`
- [ ] Create `GoogleCalendarService.cs` — `GetAuthorizationUrl`: build Google OAuth URL with fixed scope + state=appointmentId; `CreateEventAsync`: exchange code via `HttpClient.PostAsync`, load appointment from DB, create Google Calendar event via API v3
- [ ] Create `OutlookCalendarService.cs` — `GetAuthorizationUrl`: build Microsoft Identity URL; `CreateEventAsync`: exchange code via `HttpClient.PostAsync`, load appointment, create event via Microsoft Graph `POST /me/events`
- [ ] Create `CalendarController.cs` — `GET /{provider}/init` with `[Authorize]`; `GET /{provider}/callback` (no auth); resolve keyed service by provider; hardcoded redirect targets; error catch → redirect with `calendarError=true`
- [ ] Modify `ServiceCollectionExtensions.cs` — `Configure<CalendarOptions>`, `AddHttpClient<GoogleCalendarService>`, `AddHttpClient<OutlookCalendarService>`, keyed scoped DI
- [ ] Modify `appsettings.json` + `appsettings.Development.json` — add `Calendar` config section with empty credential placeholders
- [ ] Confirm `dotnet build` passes with zero errors
