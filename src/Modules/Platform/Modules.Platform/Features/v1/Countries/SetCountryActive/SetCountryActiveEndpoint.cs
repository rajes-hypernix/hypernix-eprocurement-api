using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Countries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Countries.SetCountryActive;

public static class SetCountryActiveEndpoint
{
    internal static RouteHandlerBuilder MapSetCountryActiveEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/countries/{id:guid}/active",
                async (Guid id, SetCountryActiveBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new SetCountryActiveCommand(id, body.IsActive), ct);
                    return Results.Ok(result);
                })
            .WithName("SetCountryActive")
            .WithSummary("Activate or deactivate a country")
            .RequirePermission(PlatformPermissions.Lookups.Manage);
    }

    private sealed record SetCountryActiveBody(bool IsActive);
}
