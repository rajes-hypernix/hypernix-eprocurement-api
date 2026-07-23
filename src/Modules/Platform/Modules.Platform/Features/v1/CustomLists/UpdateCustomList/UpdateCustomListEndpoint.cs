using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.CustomLists;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.CustomLists.UpdateCustomList;

public static class UpdateCustomListEndpoint
{
    internal static RouteHandlerBuilder MapUpdateCustomListEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/custom-lists/{id:guid}",
                async (Guid id, UpdateCustomListBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new UpdateCustomListCommand(id, body.Name), ct);
                    return Results.Ok(result);
                })
            .WithName("UpdateCustomList")
            .WithSummary("Update a custom list name")
            .RequirePermission(PlatformPermissions.CustomLists.Manage);
    }

    private sealed record UpdateCustomListBody(string Name);
}
