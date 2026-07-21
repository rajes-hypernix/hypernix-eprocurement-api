using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Awards;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Awards.GetAward;

public static class GetAwardEndpoint
{
    internal static RouteHandlerBuilder MapGetAwardEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/rfqs/{rfqId:guid}/award",
                async (Guid rfqId, IMediator mediator, CancellationToken ct) =>
                {
                    var award = await mediator.Send(new GetAwardQuery(rfqId), ct);
                    return award is null ? Results.NoContent() : Results.Ok(award);
                })
            .WithName("GetAward")
            .WithSummary("Get the award for an RFQ, if one has been submitted")
            .RequirePermission(SourcingPermissions.Award.View);
    }
}
