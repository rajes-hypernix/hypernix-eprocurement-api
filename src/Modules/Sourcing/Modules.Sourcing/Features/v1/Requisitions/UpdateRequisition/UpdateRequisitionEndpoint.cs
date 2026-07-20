using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.UpdateRequisition;

public static class UpdateRequisitionEndpoint
{
    internal static RouteHandlerBuilder MapUpdateRequisitionEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/requisitions/{requisitionId:guid}",
                async (Guid requisitionId, UpdateRequisitionCommand body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    var command = body with { RequisitionId = requisitionId };
                    return Results.Ok(await mediator.Send(command, ct));
                })
            .WithName("UpdateRequisition")
            .WithSummary("Update a purchase requisition")
            .RequirePermission(SourcingPermissions.Requisitions.Manage);
    }
}
