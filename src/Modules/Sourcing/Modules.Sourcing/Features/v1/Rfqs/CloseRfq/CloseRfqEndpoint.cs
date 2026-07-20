using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.CloseRfq;

public static class CloseRfqEndpoint
{
    internal static RouteHandlerBuilder MapCloseRfqEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/rfqs/{rfqId:guid}/close",
                async (Guid rfqId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new CloseRfqCommand(rfqId), ct)))
            .WithName("CloseRfq")
            .WithSummary("Close an RFQ early")
            .RequirePermission(SourcingPermissions.Rfqs.ManageLifecycle);
    }
}
