using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Awards;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Awards.SubmitAward;

public static class SubmitAwardEndpoint
{
    internal static RouteHandlerBuilder MapSubmitAwardEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/rfqs/{rfqId:guid}/award",
                async (Guid rfqId, SubmitAwardBody body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(new SubmitAwardCommand(rfqId, body.Allocations), ct));
                })
            .WithName("SubmitAward")
            .WithSummary("Submit (or resubmit) the award for an RFQ, pending Delegation-of-Authority approval")
            .RequirePermission(SourcingPermissions.Award.Submit);
    }
}

public sealed record SubmitAwardBody(List<AwardAllocationDto> Allocations);
