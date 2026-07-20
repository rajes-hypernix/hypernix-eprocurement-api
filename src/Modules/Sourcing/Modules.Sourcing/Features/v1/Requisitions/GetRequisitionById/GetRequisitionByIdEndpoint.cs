using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.GetRequisitionById;

public static class GetRequisitionByIdEndpoint
{
    internal static RouteHandlerBuilder MapGetRequisitionByIdEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/requisitions/{requisitionId:guid}",
                (Guid requisitionId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetRequisitionByIdQuery(requisitionId), ct))
            .WithName("GetRequisitionById")
            .WithSummary("Get a purchase requisition by id")
            .RequirePermission(SourcingPermissions.Requisitions.View);
    }
}
