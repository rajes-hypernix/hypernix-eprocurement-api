using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Items.CreateItem;

public static class CreateItemEndpoint
{
    internal static RouteHandlerBuilder MapCreateItemEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/items",
                async (CreateItemCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    var id = await mediator.Send(command, ct);
                    return Results.Ok(id);
                })
            .WithName("CreateItem")
            .WithSummary("Create an item")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }
}
