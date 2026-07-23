using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Bids;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Bids.GetRfqForBidding;

public static class GetRfqForBiddingEndpoint
{
    internal static RouteHandlerBuilder MapGetRfqForBiddingEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/rfqs/{rfqId:guid}/for-bidding",
                (Guid rfqId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetRfqForBiddingQuery(rfqId), ct))
            .WithName("GetRfqForBidding")
            .WithSummary("Get an RFQ's lines/questions for the vendor bid form — vendor-portal only.")
            .RequirePermission(SourcingPermissions.Bids.ViewMine);
    }
}
