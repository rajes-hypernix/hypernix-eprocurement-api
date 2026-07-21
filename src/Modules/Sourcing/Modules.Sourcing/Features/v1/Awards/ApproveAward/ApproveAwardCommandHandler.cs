using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Awards;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Awards.ApproveAward;

/// <summary>
/// Approving an award settles PR-line provenance exactly like <c>CancelRfq</c> does — but towards
/// <c>Awarded</c> for lines that received an allocation, and back to <c>Open</c> (link Returned)
/// for lines that didn't. No <c>PurchaseOrder</c> is created — that's a future Procurement module.
/// </summary>
public sealed class ApproveAwardCommandHandler(SourcingDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<ApproveAwardCommand, AwardDto>
{
    public async ValueTask<AwardDto> Handle(ApproveAwardCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rfq = await dbContext.Rfqs
            .Include(r => r.Invitations)
            .Include(r => r.Events)
            .FirstOrDefaultAsync(r => r.Id == command.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {command.RfqId} not found.");

        var award = await dbContext.Awards
            .FirstOrDefaultAsync(a => a.RfqId == command.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"No award has been submitted for RFQ {command.RfqId}.");

        var now = DateTime.UtcNow;
        string approverUserId = currentUser.GetUserId().ToString();

        award.Approve(approverUserId, now);
        rfq.MarkAwarded(now, approverUserId);

        await SettleProvenanceAsync(rfq, award, now, cancellationToken).ConfigureAwait(false);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return AwardDtoMapper.ToDto(award);
    }

    private async Task SettleProvenanceAsync(Rfq rfq, Award award, DateTime now, CancellationToken cancellationToken)
    {
        var activeLinks = await dbContext.PrLineSourcings
            .Where(s => s.RfqId == rfq.Id && s.LinkStatus == LinkStatus.Active)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (activeLinks.Count == 0)
        {
            return;
        }

        var awardedLineCodes = award.Allocations.Select(a => a.RfqLineCode).ToHashSet(StringComparer.Ordinal);

        var lineIds = activeLinks.Select(l => l.PrLineId).Distinct().ToList();
        var purchaseRequisitions = await dbContext.PurchaseRequisitions
            .Include(p => p.Lines)
            .Where(p => p.Lines.Any(l => lineIds.Contains(l.Id)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var link in activeLinks)
        {
            var pr = purchaseRequisitions.FirstOrDefault(p => p.HasLine(link.PrLineId));
            if (pr is null)
            {
                continue;
            }

            if (awardedLineCodes.Contains(link.RfqLineCode))
            {
                // Link stays Active — it's the fulfilled provenance record now that the PR line is Awarded (terminal).
                pr.MarkLineAwarded(link.PrLineId, now);
            }
            else
            {
                pr.ReturnLineFromRfq(link.PrLineId, "Not awarded", now);
                link.MarkReturned("Not awarded", now);
            }
        }
    }
}
