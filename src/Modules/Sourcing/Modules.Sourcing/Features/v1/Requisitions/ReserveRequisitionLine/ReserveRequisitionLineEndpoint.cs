using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.ReserveRequisitionLine;

public static class ReserveRequisitionLineEndpoint
{
    internal static RouteHandlerBuilder MapReserveRequisitionLineEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/requisitions/{requisitionId:guid}/lines/{lineId:guid}/reserve",
                async (Guid requisitionId, Guid lineId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ReserveRequisitionLineCommand(requisitionId, lineId), ct)))
            .WithName("ReserveRequisitionLine")
            .WithSummary("Reserve a requisition line into an RFQ draft workspace")
            .RequirePermission(SourcingPermissions.Requisitions.Manage);
    }
}
