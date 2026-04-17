using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data.Interceptors;

/// <summary>
/// Blocks EF Core update and delete operations on <see cref="AuditLog"/> entities,
/// enforcing the immutable append-only contract required by DR-008 and HIPAA NFR-007.
/// Fires before both synchronous and asynchronous SaveChanges calls.
/// </summary>
public sealed class AuditLogImmutabilityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ThrowIfAuditLogMutated(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ThrowIfAuditLogMutated(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ThrowIfAuditLogMutated(DbContext? context)
    {
        if (context is null) return;

        var mutated = context.ChangeTracker
            .Entries<AuditLog>()
            .Any(e => e.State is EntityState.Modified or EntityState.Deleted);

        if (mutated)
            throw new InvalidOperationException(
                "AuditLog records are immutable. Update and delete operations are prohibited per DR-008.");
    }
}
