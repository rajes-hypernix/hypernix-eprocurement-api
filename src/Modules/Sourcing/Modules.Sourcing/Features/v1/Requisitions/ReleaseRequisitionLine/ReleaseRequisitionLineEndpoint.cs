using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.ReleaseRequisitionLine;

public static class ReleaseRequisitionLineEndpoint
{
    internal static RouteHandlerBuilder MapReleaseRequisitionLineEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/requisitions/{requisitionId:guid}/lines/{lineId:guid}/release",
                async (Guid requisitionId, Guid lineId, ReasonBody? body, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ReleaseRequisitionLineCommand(requisitionId, lineId, body?.Reason), ct)))
            .WithName("ReleaseRequisitionLine")
            .WithSummary("Release a requisition line for re-sourcing (e.g. no quotes)")
            .RequirePermission(SourcingPermissions.Requisitions.Manage);
    }
}

public sealed record ReasonBody(string? Reason);
