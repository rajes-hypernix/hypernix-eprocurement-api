using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.ReopenRequisitionLine;

public static class ReopenRequisitionLineEndpoint
{
    internal static RouteHandlerBuilder MapReopenRequisitionLineEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/requisitions/{requisitionId:guid}/lines/{lineId:guid}/reopen",
                async (Guid requisitionId, Guid lineId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ReopenRequisitionLineCommand(requisitionId, lineId), ct)))
            .WithName("ReopenRequisitionLine")
            .WithSummary("Reopen a cancelled requisition line")
            .RequirePermission(SourcingPermissions.Requisitions.Manage);
    }
}
