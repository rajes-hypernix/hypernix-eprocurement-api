using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.Ordering;

public static class GetEligibleLinesForOrderingEndpoint
{
    internal static RouteHandlerBuilder MapGetEligibleLinesForOrderingEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/requisitions/order-builder/eligible-lines",
                async (IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new GetEligibleLinesForOrderingQuery(), ct)))
            .WithName("GetEligibleLinesForOrdering")
            .WithSummary("List requisition lines eligible for the direct-order builder workspace")
            .RequirePermission(SourcingPermissions.Requisitions.View);
    }
}
