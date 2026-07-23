using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Auditing.Contracts.Authorization;
using FSH.Modules.Auditing.Contracts.Dtos;
using FSH.Modules.Auditing.Contracts.v1.GetEntityChangeHistory;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Auditing.Features.v1.GetEntityChangeHistory;

public static class GetEntityChangeHistoryEndpoint
{
    public static RouteHandlerBuilder MapGetEntityChangeHistoryEndpoint(this IEndpointRouteBuilder group)
    {
        return group.MapGet(
                "/entity-changes/{entityId:guid}",
                async (Guid entityId, int? take, IMediator mediator, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await mediator.Send(
                        new GetEntityChangeHistoryQuery
                        {
                            EntityId = entityId,
                            Take = take ?? 50,
                        },
                        cancellationToken)))
            .WithName("GetEntityChangeHistory")
            .WithSummary("Get entity change history by entity id")
            .WithDescription(
                "On-demand EntityChange audit trail for one aggregate. Not included in domain list APIs — call when the user opens History.")
            .RequirePermission(AuditingPermissions.AuditTrails.View)
            .Produces<IReadOnlyList<AuditDetailDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}
