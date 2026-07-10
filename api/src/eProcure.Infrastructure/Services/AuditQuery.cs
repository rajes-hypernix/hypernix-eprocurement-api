using eProcure.Application.Audit;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

public sealed class AuditQuery(AppDbContext db) : IAuditQuery
{
    public async Task<IReadOnlyList<AuditEntryDto>> ListForAsync(string entityType, string entityId, CancellationToken ct = default)
    {
        var rows = await db.AuditEntries.AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.UtcTimestamp)
            .ToListAsync(ct);
        return rows.Select(a => new AuditEntryDto(
            a.EntityType, a.EntityId, a.Action, a.Before, a.After, a.ActorName, a.UtcTimestamp)).ToList();
    }
}
