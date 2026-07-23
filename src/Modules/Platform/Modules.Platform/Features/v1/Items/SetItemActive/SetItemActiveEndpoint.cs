using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Items.SetItemActive;

public static class SetItemActiveEndpoint
{
    internal static RouteHandlerBuilder MapSetItemActiveEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/items/{id:guid}/active",
                async (Guid id, SetItemActiveBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new SetItemActiveCommand(id, body.IsActive), ct);
                    return Results.Ok(result);
                })
            .WithName("SetItemActive")
            .WithSummary("Activate or deactivate an item")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }

    private sealed record SetItemActiveBody(bool IsActive);
}
