using FSH.Framework.Core.Context;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Bids;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Features.v1.Bids;
using FSH.Modules.Sourcing.Services;
using Mediator;

namespace FSH.Modules.Sourcing.Features.v1.Bids.GetRfqForBidding;

/// <summary>Vendor-scoped RFQ read for the bid form — same ownership guard as the other Bid slices.</summary>
public sealed class GetRfqForBiddingQueryHandler(SourcingDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<GetRfqForBiddingQuery, RfqForBiddingDto>
{
    public async ValueTask<RfqForBiddingDto> Handle(GetRfqForBiddingQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var vendorId = currentUser.RequireVendorId();

        var rfq = await BidAuthorization.RequireInvitedRfqAsync(dbContext, query.RfqId, vendorId, cancellationToken)
            .ConfigureAwait(false);

        return new RfqForBiddingDto(
            rfq.Id,
            rfq.Code,
            rfq.Title,
            rfq.Envelope.ToString(),
            rfq.Status.ToString(),
            rfq.Currency,
            rfq.OpensUtc,
            rfq.ClosesUtc,
            [.. rfq.Lines.Select(l => new RfqLineDto(l.LineCode, l.ItemCode, l.Description, l.Qty, l.Uom, l.PrRef, l.SourcePrLineIds))],
            [.. rfq.FormItems.Select(f => new FormItemDto(f.Kind, f.Group, f.Section, f.Label, f.Type, f.Required, f.ConfigJson, f.Help, f.Order))],
            rfq.TechnicalSections,
            rfq.CommercialSections);
    }
}
