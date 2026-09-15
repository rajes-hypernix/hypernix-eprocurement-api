using FSH.Framework.Core.Context;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using FSH.Modules.Sourcing.Services;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs;

internal static class RfqListLoader
{
    internal static async Task<IReadOnlyList<RfqListItemDto>> LoadAsync(
        SourcingDbContext dbContext,
        ICurrentUser currentUser,
        bool openingsOnly,
        CancellationToken cancellationToken)
    {
        var rfqQuery = dbContext.Rfqs
            .Include(r => r.Invitations)
            .Include(r => r.Lines)
            .Include(r => r.Events)
            .AsQueryable();

        if (currentUser.GetVendorId() is { } vendorId)
        {
            rfqQuery = rfqQuery.Where(r => r.Invitations.Any(i => i.VendorId == vendorId));
        }

        var rfqs = await rfqQuery
            .OrderByDescending(r => r.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        bool closedAny = false;
        foreach (var rfq in rfqs)
        {
            closedAny |= RfqCloseDue.Apply(rfq, now);
        }

        if (closedAny)
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (openingsOnly)
        {
            rfqs = [.. rfqs.Where(IsReadyToOpen)];
        }

        var rfqIds = rfqs.Select(r => r.Id).ToList();
        var bidCountsByRfq = await dbContext.Bids
            .AsNoTracking()
            .Where(b => rfqIds.Contains(b.RfqId))
            .GroupBy(b => b.RfqId)
            .Select(g => new { RfqId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RfqId, x => x.Count, cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rfqs.Select(r =>
                RfqDtoMapper.ToListItemDto(r, bidCountsByRfq.TryGetValue(r.Id, out var count) ? count : 0))
        ];
    }

    private static bool IsReadyToOpen(Rfq rfq) =>
        rfq.Status is RfqStatus.Closed or RfqStatus.Evaluation or RfqStatus.Awarded;
}
