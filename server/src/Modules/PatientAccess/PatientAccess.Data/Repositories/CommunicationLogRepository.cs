using Microsoft.EntityFrameworkCore;
using PatientAccess.Application.Repositories;
using PatientAccess.Data.Entities;
using PatientAccess.Domain.Enums;

namespace PatientAccess.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ICommunicationLogRepository"/>.
/// </summary>
public sealed class CommunicationLogRepository : ICommunicationLogRepository
{
    private readonly PropelIQDbContext _dbContext;

    public CommunicationLogRepository(PropelIQDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AddAsync(CommunicationLogEntry entry, CancellationToken ct = default)
    {
        var log = new CommunicationLog
        {
            Id            = Guid.NewGuid(),
            PatientId     = entry.PatientId,
            AppointmentId = entry.AppointmentId,
            Channel       = entry.Channel,
            Status        = entry.Status,
            AttemptCount  = entry.AttemptCount,
            PdfBytes      = entry.PdfBytes,
            CreatedAt     = DateTime.UtcNow,
        };

        _dbContext.CommunicationLogs.Add(log);
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetConfirmationPdfBytesAsync(
        Guid appointmentId,
        CancellationToken ct = default)
    {
        return await _dbContext.CommunicationLogs
            .Where(c => c.AppointmentId == appointmentId
                     && c.Channel == CommunicationChannel.Email
                     && c.PdfBytes != null)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => c.PdfBytes)
            .FirstOrDefaultAsync(ct);
    }
}
