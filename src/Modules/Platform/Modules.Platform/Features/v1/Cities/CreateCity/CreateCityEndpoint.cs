using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Cities;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Cities.CreateCity;

public static class CreateCityEndpoint
{
    internal static RouteHandlerBuilder MapCreateCityEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/cities",
                async (CreateCityCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    var id = await mediator.Send(command, ct);
                    return Results.Ok(id);
                })
            .WithName("CreateCity")
            .WithSummary("Create a city")
            .RequirePermission(PlatformPermissions.Lookups.Manage);
    }
}
