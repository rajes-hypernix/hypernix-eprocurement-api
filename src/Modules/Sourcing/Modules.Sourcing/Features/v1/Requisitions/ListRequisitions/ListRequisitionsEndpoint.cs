using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.ListRequisitions;

public static class ListRequisitionsEndpoint
{
    internal static RouteHandlerBuilder MapListRequisitionsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/requisitions",
                (IMediator mediator, CancellationToken ct) => mediator.Send(new ListRequisitionsQuery(), ct))
            .WithName("ListRequisitions")
            .WithSummary("List purchase requisitions")
            .RequirePermission(SourcingPermissions.Requisitions.View);
    }
}
