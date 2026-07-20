using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.ReleaseRfq;

/// <summary>
/// Writes PrLineSourcing lineage and flips source PR lines InDraftRfq -&gt; InRfq in the same
/// transaction as the RFQ Draft -&gt; Open transition and the Released event (all-or-nothing).
/// </summary>
public sealed class ReleaseRfqCommandHandler(SourcingDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<ReleaseRfqCommand, Guid>
{
    public async ValueTask<Guid> Handle(ReleaseRfqCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rfq = await dbContext.Rfqs
            .Include(r => r.Invitations)
            .Include(r => r.Events)
            .FirstOrDefaultAsync(r => r.Id == command.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {command.RfqId} not found.");

        var sourceLineIds = rfq.Lines
            .SelectMany(l => l.SourcePrLineIds)
            .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();

        if (sourceLineIds.Count > 0)
        {
            var purchaseRequisitions = await dbContext.PurchaseRequisitions
                .Include(p => p.Lines)
                .Where(p => p.Lines.Any(l => sourceLineIds.Contains(l.Id)))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var now = DateTime.UtcNow;
            foreach (var rfqLine in rfq.Lines)
            {
                foreach (string sourceIdText in rfqLine.SourcePrLineIds)
                {
                    if (!Guid.TryParse(sourceIdText, out var lineId))
                    {
                        continue;
                    }

                    var pr = purchaseRequisitions.FirstOrDefault(p => p.HasLine(lineId));
                    if (pr is null)
                    {
                        continue;
                    }

                    var prLine = pr.GetLine(lineId);
                    pr.ReleaseLineToRfq(lineId, rfq.Code, now);
                    dbContext.PrLineSourcings.Add(PrLineSourcing.Create(lineId, rfq.Id, rfqLine.LineCode, prLine.Qty, now));
                }
            }
        }

        rfq.MarkReleased(DateTime.UtcNow, currentUser.IsAuthenticated() ? currentUser.GetUserId().ToString() : null);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rfq.Id;
    }
}
