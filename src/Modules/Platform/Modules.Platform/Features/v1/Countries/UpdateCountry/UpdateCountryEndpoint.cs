using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Countries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Countries.UpdateCountry;

public static class UpdateCountryEndpoint
{
    internal static RouteHandlerBuilder MapUpdateCountryEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/countries/{id:guid}",
                async (Guid id, UpdateCountryBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new UpdateCountryCommand(id, body.Code, body.Name), ct);
                    return Results.Ok(result);
                })
            .WithName("UpdateCountry")
            .WithSummary("Update a country")
            .RequirePermission(PlatformPermissions.Lookups.Manage);
    }

    private sealed record UpdateCountryBody(string Code, string Name);
}
