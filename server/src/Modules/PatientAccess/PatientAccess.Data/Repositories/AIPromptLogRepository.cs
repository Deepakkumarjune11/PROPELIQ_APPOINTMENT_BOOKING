using Microsoft.EntityFrameworkCore;
using PatientAccess.Application.Repositories;
using PatientAccess.Data.Entities;

namespace PatientAccess.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAIPromptLogRepository"/>.
/// Writes sanitised metadata-only audit records to the <c>ai_prompt_log</c> table (AIR-S03).
/// </summary>
public sealed class AIPromptLogRepository : IAIPromptLogRepository
{
    private readonly PropelIQDbContext _db;

    public AIPromptLogRepository(PropelIQDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(AIPromptLogEntry entry, CancellationToken ct = default)
    {
        _db.AiPromptLogs.Add(new AIPromptLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            ModelProvider = entry.ModelProvider,
            DeploymentName = entry.DeploymentName,
            RequestSummary = entry.RequestSummary,
            ResponseSummary = entry.ResponseSummary,
            IsComplete = entry.IsComplete
        });

        await _db.SaveChangesAsync(ct);
    }
}
