using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.UnreserveRequisitionLine;

public static class UnreserveRequisitionLineEndpoint
{
    internal static RouteHandlerBuilder MapUnreserveRequisitionLineEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/requisitions/{requisitionId:guid}/lines/{lineId:guid}/unreserve",
                async (Guid requisitionId, Guid lineId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new UnreserveRequisitionLineCommand(requisitionId, lineId), ct)))
            .WithName("UnreserveRequisitionLine")
            .WithSummary("Remove a requisition line from an RFQ draft workspace")
            .RequirePermission(SourcingPermissions.Requisitions.Manage);
    }
}
