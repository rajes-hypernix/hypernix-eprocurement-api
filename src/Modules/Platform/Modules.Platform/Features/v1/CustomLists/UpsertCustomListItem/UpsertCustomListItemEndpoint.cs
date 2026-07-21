using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.CustomLists;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.CustomLists.UpsertCustomListItem;

public static class UpsertCustomListItemEndpoint
{
    internal static RouteHandlerBuilder MapUpsertCustomListItemEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/custom-lists/{listKey}/items",
                async (string listKey, UpsertCustomListItemBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var id = await mediator.Send(
                        new UpsertCustomListItemCommand(listKey, body.Code, body.Label, body.SortOrder, body.IsActive),
                        ct);
                    return Results.Ok(id);
                })
            .WithName("UpsertCustomListItem")
            .WithSummary("Create or update a custom list item")
            .RequirePermission(PlatformPermissions.CustomLists.Manage);
    }

    private sealed record UpsertCustomListItemBody(
        string Code,
        string Label,
        int SortOrder = 0,
        bool IsActive = true);
}
