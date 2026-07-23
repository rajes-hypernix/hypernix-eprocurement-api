using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Items.ListItems;

public static class ListItemsEndpoint
{
    internal static RouteHandlerBuilder MapListItemsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/items",
                async (bool? activeOnly, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ListItemsQuery(activeOnly ?? true), ct)))
            .WithName("ListItems")
            .WithSummary("List items")
            .RequirePermission(PlatformPermissions.Configuration.View);
    }
}
