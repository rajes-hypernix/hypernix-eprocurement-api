using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Bids;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Bids.SubmitBid;

public static class SubmitBidEndpoint
{
    internal static RouteHandlerBuilder MapSubmitBidEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/rfqs/{rfqId:guid}/my-bid/submit",
                (Guid rfqId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new SubmitBidCommand(rfqId), ct))
            .WithName("SubmitBid")
            .WithSummary("Submit the calling vendor's bid on an RFQ — vendor-portal only.")
            .RequirePermission(SourcingPermissions.Bids.Respond);
    }
}
