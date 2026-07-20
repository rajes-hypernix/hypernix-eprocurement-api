using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.SubmitRequisition;

public static class SubmitRequisitionEndpoint
{
    internal static RouteHandlerBuilder MapSubmitRequisitionEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/requisitions/{requisitionId:guid}/submit",
                async (Guid requisitionId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new SubmitRequisitionCommand(requisitionId), ct)))
            .WithName("SubmitRequisition")
            .WithSummary("Submit a purchase requisition")
            .RequirePermission(SourcingPermissions.Requisitions.Manage);
    }
}
