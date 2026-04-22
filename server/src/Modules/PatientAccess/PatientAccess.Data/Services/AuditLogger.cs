using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PatientAccess.Application.Infrastructure;
using PatientAccess.Data.Entities;
using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Services;

/// <summary>
/// Computes a SHA-256 hash chain and stages an <see cref="AuditLog"/> entry on the
/// DbContext change tracker without calling SaveChangesAsync (AC-5, US_026, TR-018).
/// The caller's SaveChangesAsync commits the audit entry atomically with the main operation.
/// </summary>
public sealed class AuditLogger(
    PropelIQDbContext db,
    IHttpContextAccessor httpContextAccessor) : IAuditLogger
{
    public async Task LogAsync(
        AuditActorType actorType,
        Guid actorId,
        AuditActionType actionType,
        string targetEntityType,
        Guid targetEntityId,
        object? oldValues,
        object? newValues,
        CancellationToken ct = default)
    {
        // Retrieve the last chain hash for predecessor linkage (AC-4, TR-018).
        // AsNoTracking — read-only; no change-tracker overhead.
        var previousHash = await db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.OccurredAt)
            .Select(a => a.ChainHash)
            .FirstOrDefaultAsync(ct);   // null → genesis entry

        var id          = Guid.NewGuid();
        var occurredAt  = DateTime.UtcNow;

        // IP capture — falls back to "system" in background-job contexts where HttpContext is null.
        var ipAddress = httpContextAccessor.HttpContext?
            .Connection.RemoteIpAddress?.ToString() ?? "system";

        // SHA-256 hash chain (TR-018): genesis sentinel avoids null ambiguity.
        var chainInput = $"{id}|{actorId}|{actionType}|{targetEntityType}|{targetEntityId}|{occurredAt:O}|{previousHash ?? "GENESIS"}";
        var chainHash  = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(chainInput))).ToLowerInvariant();

        var entry = new AuditLog
        {
            Id               = id,
            ActorId          = actorId,
            ActorType        = actorType,
            ActionType       = actionType,
            TargetEntityId   = targetEntityId,
            TargetEntityType = targetEntityType,
            // Details kept for backward-compat; structured state lives in OldValues/NewValues.
            Details          = "{}",
            IpAddress        = ipAddress,
            // SECURITY: callers must never pass AuthCredentials or raw passwords (OWASP A02, NFR-007).
            OldValues        = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues        = newValues is null ? null : JsonSerializer.Serialize(newValues),
            PreviousHash     = previousHash,
            ChainHash        = chainHash,
            OccurredAt       = occurredAt,
        };

        // Stage on change tracker — no SaveChangesAsync here (AC-5).
        db.AuditLogs.Add(entry);
    }
}
