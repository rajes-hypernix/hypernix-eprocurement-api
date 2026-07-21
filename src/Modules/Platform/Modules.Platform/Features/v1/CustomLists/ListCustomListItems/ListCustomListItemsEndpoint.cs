using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.CustomLists;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.CustomLists.ListCustomListItems;

public static class ListCustomListItemsEndpoint
{
    internal static RouteHandlerBuilder MapListCustomListItemsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/custom-lists/{listKey}/items",
                async (string listKey, bool? activeOnly, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new ListCustomListItemsQuery(listKey, activeOnly ?? true), ct);
                    return Results.Ok(result);
                })
            .WithName("ListCustomListItems")
            .WithSummary("List items for a custom list key")
            .RequirePermission(PlatformPermissions.CustomLists.View);
    }
}
