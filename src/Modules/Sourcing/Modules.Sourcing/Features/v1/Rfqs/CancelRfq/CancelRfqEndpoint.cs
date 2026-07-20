using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.CancelRfq;

public static class CancelRfqEndpoint
{
    internal static RouteHandlerBuilder MapCancelRfqEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/rfqs/{rfqId:guid}/cancel",
                async (Guid rfqId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new CancelRfqCommand(rfqId), ct)))
            .WithName("CancelRfq")
            .WithSummary("Cancel an RFQ")
            .RequirePermission(SourcingPermissions.Rfqs.ManageLifecycle);
    }
}
