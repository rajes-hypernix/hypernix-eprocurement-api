using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Locations.SetLocationActive;

public static class SetLocationActiveEndpoint
{
    internal static RouteHandlerBuilder MapSetLocationActiveEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/locations/{id:guid}/active",
                async (Guid id, SetLocationActiveBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new SetLocationActiveCommand(id, body.IsActive), ct);
                    return Results.Ok(result);
                })
            .WithName("SetLocationActive")
            .WithSummary("Activate or deactivate a location")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }

    private sealed record SetLocationActiveBody(bool IsActive);
}
