using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.CancelRequisitionLine;

public static class CancelRequisitionLineEndpoint
{
    internal static RouteHandlerBuilder MapCancelRequisitionLineEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/requisitions/{requisitionId:guid}/lines/{lineId:guid}/cancel",
                async (Guid requisitionId, Guid lineId, ReasonBody? body, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new CancelRequisitionLineCommand(requisitionId, lineId, body?.Reason), ct)))
            .WithName("CancelRequisitionLine")
            .WithSummary("Cancel a requisition line")
            .RequirePermission(SourcingPermissions.Requisitions.Manage);
    }
}

public sealed record ReasonBody(string? Reason);
