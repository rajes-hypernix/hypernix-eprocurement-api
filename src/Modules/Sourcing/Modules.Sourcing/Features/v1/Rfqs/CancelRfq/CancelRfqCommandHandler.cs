using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.CancelRfq;

/// <summary>Cancelling an RFQ returns its sourced PR lines to Open and closes their PrLineSourcing links.</summary>
public sealed class CancelRfqCommandHandler(SourcingDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<CancelRfqCommand, Guid>
{
    public async ValueTask<Guid> Handle(CancelRfqCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rfq = await dbContext.Rfqs
            .Include(r => r.Invitations)
            .Include(r => r.Events)
            .FirstOrDefaultAsync(r => r.Id == command.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {command.RfqId} not found.");

        var now = DateTime.UtcNow;

        var activeLinks = await dbContext.PrLineSourcings
            .Where(s => s.RfqId == rfq.Id && s.LinkStatus == LinkStatus.Active)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (activeLinks.Count > 0)
        {
            var lineIds = activeLinks.Select(l => l.PrLineId).Distinct().ToList();
            var purchaseRequisitions = await dbContext.PurchaseRequisitions
                .Include(p => p.Lines)
                .Where(p => p.Lines.Any(l => lineIds.Contains(l.Id)))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var link in activeLinks)
            {
                var pr = purchaseRequisitions.FirstOrDefault(p => p.HasLine(link.PrLineId));
                if (pr is not null)
                {
                    pr.ReturnLineFromRfq(link.PrLineId, "RFQ cancelled", now);
                }

                link.MarkCancelled("RFQ cancelled", now);
            }
        }

        rfq.CancelRfq(now, currentUser.IsAuthenticated() ? currentUser.GetUserId().ToString() : null);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rfq.Id;
    }
}
