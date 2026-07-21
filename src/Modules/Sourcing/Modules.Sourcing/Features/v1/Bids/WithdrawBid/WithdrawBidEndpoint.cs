using FSH.Modules.Sourcing.Contracts.v1.Bids;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Bids.WithdrawBid;

public static class WithdrawBidEndpoint
{
    internal static RouteHandlerBuilder MapWithdrawBidEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/rfqs/{rfqId:guid}/my-bid/withdraw",
                (Guid rfqId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new WithdrawBidCommand(rfqId), ct))
            .WithName("WithdrawBid")
            .WithSummary("Withdraw the calling vendor's submitted bid on an RFQ — vendor-portal only.");
    }
}
