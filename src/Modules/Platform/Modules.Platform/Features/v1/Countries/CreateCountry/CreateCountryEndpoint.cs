using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Countries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Countries.CreateCountry;

public static class CreateCountryEndpoint
{
    internal static RouteHandlerBuilder MapCreateCountryEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/countries",
                async (CreateCountryCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    var id = await mediator.Send(command, ct);
                    return Results.Ok(id);
                })
            .WithName("CreateCountry")
            .WithSummary("Create a country")
            .RequirePermission(PlatformPermissions.Lookups.Manage);
    }
}
