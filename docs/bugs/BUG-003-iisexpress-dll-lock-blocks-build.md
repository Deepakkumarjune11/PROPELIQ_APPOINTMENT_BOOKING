# Bug Fix Task - BUG-003

## Bug Report Reference

- **Bug ID**: BUG-003
- **Source**: `dotnet build` from terminal during active IIS Express debug session

---

## Bug Summary

### Issue Classification

- **Priority**: Medium
- **Severity**: Developer experience — code changes not reflected in running server without manual stop/restart in Visual Studio
- **Affected Version**: HEAD (main)
- **Environment**: Windows Dev — Visual Studio + IIS Express, .NET 8

### Steps to Reproduce

1. Start the API via Visual Studio `F5` (IIS Express, PID e.g. 4576)
2. Edit any `.cs` file in the solution
3. Run `dotnet build` from a PowerShell terminal
4. **Expected**: Build succeeds and server picks up the changes (or at least build completes cleanly)
5. **Actual**: Build fails with `MSB3027` / `MSB3021` — cannot copy updated DLLs because IIS Express Worker Process (and Visual Studio itself) hold file locks:

**Error Output**:

```text
error MSB3027: Could not copy "...PatientAccess.Data.dll" to "bin\Debug\net8.0\PatientAccess.Data.dll".
Exceeded retry count of 10. Failed.
The file is locked by: "Microsoft Visual Studio (15996), IIS Express Worker Process (4576)"

error MSB3021: Unable to copy file "...PatientAccess.Presentation.dll" to
"bin\Debug\net8.0\PatientAccess.Presentation.dll". The process cannot access
the file because it is being used by another process.
```

### Root Cause Analysis

- **Component**: IIS Express dev hosting model
- **Cause**: IIS Express loads the application DLLs in-process and holds them open for the lifetime of the debug session. `dotnet build` (MSBuild) attempts to copy newly compiled DLLs into the output directory but cannot because IIS Express has exclusive file handles. This is a fundamental Windows in-process hosting limitation.

  Unlike `dotnet watch` (which uses `--no-build` + shadow copy), a direct `dotnet build` from a terminal while IIS Express is running will always fail to replace locked outputs.

### Impact Assessment

- **Affected Features**: Developer workflow — all code changes require Visual Studio restart to take effect
- **User Impact**: Developer — slower iteration cycle; risk of testing against stale binaries
- **Data Integrity Risk**: No
- **Security Implications**: None

---

## Fix Overview

Two complementary strategies:

1. **Short-term (process)**: Always use **Visual Studio Rebuild** (Ctrl+Shift+B after Shift+F5) — never `dotnet build` from terminal while IIS Express is running.
2. **Long-term (structural)**: Switch dev hosting from IIS Express to `dotnet run` / `dotnet watch` which uses out-of-process hosting and supports hot-reload. Update `launchSettings.json` to add a `dotnetCLI` profile.

---

## Fix Dependencies

- None

---

## Impacted Components

### Dev tooling

- `server/src/PropelIQ.Api/Properties/launchSettings.json` — MODIFIED (add `dotnetCLI` / `http` profile)

---

## Expected Changes

| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/PropelIQ.Api/Properties/launchSettings.json` | Add `http` profile using `dotnet run` instead of IIS Express for terminal-based workflow |

### Suggested `launchSettings.json` addition

```json
"http": {
  "commandName": "Project",
  "dotnetRunMessages": true,
  "launchBrowser": false,
  "applicationUrl": "https://localhost:44397;http://localhost:8080",
  "environmentVariables": {
    "ASPNETCORE_ENVIRONMENT": "Development"
  }
}
```

---

## Implementation Plan

1. Add `http` profile to `launchSettings.json`
2. Document in README: use `dotnet watch --project server/src/PropelIQ.Api` for terminal-based development
3. Keep the IIS Express profile for Visual Studio F5 debugging with breakpoints

---

## Regression Prevention Strategy

- [ ] Verify `dotnet watch` rebuilds and reloads on `.cs` file save without lock errors
- [ ] Verify IIS Express F5 debug session still works from Visual Studio

---

## Rollback Procedure

1. Remove the added `http` profile from `launchSettings.json`

---

## External References

- [ASP.NET Core — Use dotnet watch](https://learn.microsoft.com/en-us/aspnet/core/tutorials/dotnet-watch)
- [IIS Express in-process hosting](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/in-process-hosting)

---

## Build Commands

```powershell
# Terminal-based hot-reload (no IIS Express lock)
cd server/src/PropelIQ.Api
dotnet watch
```

---

## Implementation Validation Strategy

- [ ] `dotnet build` completes without MSB3027 errors when IIS Express is not running
- [ ] `dotnet watch` restarts server automatically on `.cs` file save
- [ ] Visual Studio F5 + IIS Express still works for breakpoint debugging

## Implementation Checklist

- [ ] `http` profile added to `launchSettings.json`
- [ ] README updated with `dotnet watch` instructions
