using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.ReleaseRfq;

public static class ReleaseRfqEndpoint
{
    internal static RouteHandlerBuilder MapReleaseRfqEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/rfqs/{rfqId:guid}/release",
                async (Guid rfqId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ReleaseRfqCommand(rfqId), ct)))
            .WithName("ReleaseRfq")
            .WithSummary("Release an RFQ draft (Draft -> Open)")
            .RequirePermission(SourcingPermissions.Rfqs.ManageLifecycle);
    }
}
