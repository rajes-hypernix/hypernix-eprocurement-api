using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.CancelRequisition;

public static class CancelRequisitionEndpoint
{
    internal static RouteHandlerBuilder MapCancelRequisitionEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/requisitions/{requisitionId:guid}/cancel",
                async (Guid requisitionId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new CancelRequisitionCommand(requisitionId), ct)))
            .WithName("CancelRequisition")
            .WithSummary("Cancel a purchase requisition")
            .RequirePermission(SourcingPermissions.Requisitions.Manage);
    }
}
