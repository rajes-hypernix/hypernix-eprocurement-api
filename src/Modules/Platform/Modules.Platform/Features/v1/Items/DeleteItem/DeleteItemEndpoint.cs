using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Items.DeleteItem;

public static class DeleteItemEndpoint
{
    internal static RouteHandlerBuilder MapDeleteItemEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapDelete("/items/{id:guid}",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                {
                    await mediator.Send(new DeleteItemCommand(id), ct);
                    return Results.NoContent();
                })
            .WithName("DeleteItem")
            .WithSummary("Hard-delete an item")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }
}
