using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Bids;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Bids.GetMyBid;

public static class GetMyBidEndpoint
{
    internal static RouteHandlerBuilder MapGetMyBidEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/rfqs/{rfqId:guid}/my-bid",
                async (Guid rfqId, IMediator mediator, CancellationToken ct) =>
                {
                    var bid = await mediator.Send(new GetMyBidQuery(rfqId), ct);
                    return bid is null ? Results.NoContent() : Results.Ok(bid);
                })
            .WithName("GetMyBid")
            .WithSummary("Get the calling vendor's own bid on an RFQ — vendor-portal only.")
            .RequirePermission(SourcingPermissions.Bids.ViewMine);
    }
}
