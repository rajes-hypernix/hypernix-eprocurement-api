using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.CustomLists;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.CustomLists.SetCustomListActive;

public static class SetCustomListActiveEndpoint
{
    internal static RouteHandlerBuilder MapSetCustomListActiveEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/custom-lists/{id:guid}/active",
                async (Guid id, SetCustomListActiveBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new SetCustomListActiveCommand(id, body.IsActive), ct);
                    return Results.Ok(result);
                })
            .WithName("SetCustomListActive")
            .WithSummary("Activate or deactivate a custom list")
            .RequirePermission(PlatformPermissions.CustomLists.Manage);
    }

    private sealed record SetCustomListActiveBody(bool IsActive);
}
