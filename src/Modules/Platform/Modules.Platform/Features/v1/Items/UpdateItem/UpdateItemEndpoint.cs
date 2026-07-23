using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Items.UpdateItem;

public static class UpdateItemEndpoint
{
    internal static RouteHandlerBuilder MapUpdateItemEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/items/{id:guid}",
                async (Guid id, UpdateItemBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(
                        new UpdateItemCommand(id, body.ItemCode, body.Description, body.Uom),
                        ct);
                    return Results.Ok(result);
                })
            .WithName("UpdateItem")
            .WithSummary("Update an item")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }

    private sealed record UpdateItemBody(string ItemCode, string Description, string Uom);
}
