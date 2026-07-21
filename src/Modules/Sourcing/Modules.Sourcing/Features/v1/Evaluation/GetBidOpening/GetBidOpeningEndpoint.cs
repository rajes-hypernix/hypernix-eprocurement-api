using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Evaluation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Evaluation.GetBidOpening;

public static class GetBidOpeningEndpoint
{
    internal static RouteHandlerBuilder MapGetBidOpeningEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/rfqs/{rfqId:guid}/bid-opening",
                (Guid rfqId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetBidOpeningQuery(rfqId), ct))
            .WithName("GetBidOpening")
            .WithSummary("Get the envelope-opening/finalization gate status for an RFQ")
            .RequirePermission(SourcingPermissions.Evaluation.ViewOpening);
    }
}
