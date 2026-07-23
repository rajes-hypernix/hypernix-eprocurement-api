using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Cities;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Cities.SetCityActive;

public static class SetCityActiveEndpoint
{
    internal static RouteHandlerBuilder MapSetCityActiveEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/cities/{id:guid}/active",
                async (Guid id, SetCityActiveBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new SetCityActiveCommand(id, body.IsActive), ct);
                    return Results.Ok(result);
                })
            .WithName("SetCityActive")
            .WithSummary("Activate or deactivate a city")
            .RequirePermission(PlatformPermissions.Lookups.Manage);
    }

    private sealed record SetCityActiveBody(bool IsActive);
}
