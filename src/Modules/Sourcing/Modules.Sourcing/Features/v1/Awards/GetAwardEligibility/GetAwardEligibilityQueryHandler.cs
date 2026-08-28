using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Awards;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using FSH.Modules.Sourcing.Services;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Awards.GetAwardEligibility;

public sealed class GetAwardEligibilityQueryHandler(SourcingDbContext dbContext, IMediator mediator, ICurrentUser currentUser)
    : IQueryHandler<GetAwardEligibilityQuery, AwardEligibilityDto>
{
    public async ValueTask<AwardEligibilityDto> Handle(GetAwardEligibilityQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (currentUser.GetVendorId() is not null)
        {
            throw new ForbiddenException("Vendors cannot view award workspaces.");
        }

        var rfq = await dbContext.Rfqs
            .AsNoTracking()
            .Include(r => r.Invitations)
            .Include(r => r.Lines)
            .Include(r => r.FormItems)
            .FirstOrDefaultAsync(r => r.Id == query.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {query.RfqId} not found.");

        var scores = await dbContext.TechnicalScores
            .AsNoTracking()
            .Where(s => s.RfqId == query.RfqId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool masked = !rfq.CommercialRevealed;
        var invitedOrder = rfq.Invitations.OrderBy(i => i.InvitedUtc).Select(i => i.VendorId).ToList();

        var techQ = rfq.FormItems
            .Where(i => i.Kind.Equals("question", StringComparison.OrdinalIgnoreCase)
                        && i.Group.Equals("Technical", StringComparison.OrdinalIgnoreCase))
            .OrderBy(i => i.Order)
            .Select(i => new AwardQaItemDto(i.Order, i.Label, i.Type, i.ConfigJson, i.Group))
            .ToList();
        var commQ = rfq.FormItems
            .Where(i => i.Kind.Equals("question", StringComparison.OrdinalIgnoreCase)
                        && i.Group.Equals("Commercial", StringComparison.OrdinalIgnoreCase))
            .OrderBy(i => i.Order)
            .Select(i => new AwardQaItemDto(i.Order, i.Label, i.Type, i.ConfigJson, i.Group))
            .ToList();

        var vendorRows = new List<AwardEligibilityRowDto>();
        foreach (var invitation in rfq.Invitations)
        {
            bool submitted = invitation.Status == RfqInvitationStatus.BidSubmitted;
            var committee = TechnicalEvaluation.Committee([.. scores.Where(s => s.VendorId == invitation.VendorId)]);
            bool pass = TechnicalEvaluation.Pass(committee);
            bool eligible = AwardEligibility.IsEligible(rfq, submitted, pass);
            string alias = TechnicalEvaluation.Alias(invitedOrder, invitation.VendorId);

            string? vendorName = null;
            string? vendorCode = null;
            if (!masked)
            {
                try
                {
                    var vendor = await mediator.Send(new GetVendorByIdQuery(invitation.VendorId), cancellationToken).ConfigureAwait(false);
                    vendorName = vendor.Name;
                    vendorCode = vendor.Code;
                }
                catch (NotFoundException)
                {
                    vendorName = "(unknown vendor)";
                }
            }

            vendorRows.Add(new AwardEligibilityRowDto(
                invitation.VendorId, vendorName, vendorCode, alias, masked, eligible, committee, pass));
        }

        if (!rfq.CommercialRevealed)
        {
            var sealedLines = rfq.Lines
                .Select(l => new AwardCompareLineDto(l.LineCode, l.ItemCode, l.Description, l.Qty, l.Uom, [], null))
                .ToList();
            return new AwardEligibilityDto(
                rfq.Id, rfq.Code, rfq.Title, rfq.Envelope.ToString(), rfq.Currency,
                false, rfq.TechFinalized, true,
                sealedLines, [], vendorRows, techQ, commQ, []);
        }

        var submittedBids = await dbContext.Bids
            .AsNoTracking()
            .Include(b => b.Lines)
            .Include(b => b.Answers)
            .Where(b => b.RfqId == query.RfqId && b.Submitted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var bidsByVendor = submittedBids.ToDictionary(b => b.VendorId);

        var eligibleIds = vendorRows.Where(v => v.Eligible && bidsByVendor.ContainsKey(v.VendorId))
            .Select(v => v.VendorId)
            .ToList();

        string DisplayName(Guid vendorId)
        {
            var row = vendorRows.First(v => v.VendorId == vendorId);
            return masked ? row.Alias : (row.VendorName ?? row.Alias);
        }

        var lines = rfq.Lines.Select(l =>
        {
            var opts = eligibleIds
                .Select(vid => (vid, bidLine: bidsByVendor[vid].Lines.FirstOrDefault(bl => bl.ItemCode == l.ItemCode)))
                .Where(x => x.bidLine is { Bidding: true } && x.bidLine.Price > 0)
                .Select(x => new AwardVendorOptionDto(x.vid, DisplayName(x.vid), x.bidLine!.Price, x.bidLine.Qty))
                .OrderBy(o => o.UnitPrice)
                .ToList();
            return new AwardCompareLineDto(
                l.LineCode, l.ItemCode, l.Description, l.Qty, l.Uom, opts, opts.FirstOrDefault()?.VendorId);
        }).ToList();

        var responses = eligibleIds.Select(vid =>
        {
            var answers = (bidsByVendor.GetValueOrDefault(vid)?.Answers ?? [])
                .Select(a => new AwardQaAnswerDto(a.QuestionOrder, a.Value))
                .ToList();
            return new AwardResponseDto(vid, DisplayName(vid), answers);
        }).ToList();

        var ranking = BuildRanking(rfq, eligibleIds, bidsByVendor, scores, DisplayName);

        return new AwardEligibilityDto(
            rfq.Id, rfq.Code, rfq.Title, rfq.Envelope.ToString(), rfq.Currency,
            true, rfq.TechFinalized, masked,
            lines, ranking, vendorRows, techQ, commQ, responses);
    }

    private static List<AwardRankRowDto> BuildRanking(
        Rfq rfq,
        IReadOnlyList<Guid> eligible,
        IReadOnlyDictionary<Guid, Bid> bids,
        IReadOnlyList<TechnicalScore> scores,
        Func<Guid, string> displayName)
    {
        decimal TotalFor(Guid vendorId) => rfq.Lines.Sum(l =>
        {
            var bl = bids[vendorId].Lines.FirstOrDefault(x => x.ItemCode == l.ItemCode);
            return bl is { Bidding: true } ? bl.Price * l.Qty : 0m;
        });

        var totals = eligible.ToDictionary(v => v, TotalFor);
        var lowest = totals.Values.Where(t => t > 0).DefaultIfEmpty(0).Min();
        bool dual = rfq.Envelope == RfqEnvelope.Dual;

        var rows = eligible.Select(v =>
        {
            double priceScore = totals[v] > 0 && lowest > 0
                ? Math.Round((double)(lowest / totals[v]) * 1000) / 10
                : 0;
            decimal? tech = dual
                ? TechnicalEvaluation.Committee([.. scores.Where(s => s.VendorId == v)])
                : null;
            double combined = dual
                ? Math.Round((0.7 * (double)(tech ?? 0) + 0.3 * priceScore) * 10) / 10
                : priceScore;
            return new AwardRankRowDto(v, displayName(v), tech, priceScore, combined, false);
        }).OrderByDescending(r => r.Combined).ToList();

        return [.. rows.Select((r, i) => r with { Recommended = i == 0 })];
    }
}
