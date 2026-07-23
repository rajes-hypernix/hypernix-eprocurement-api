using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Cities;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Cities.DeleteCity;

public static class DeleteCityEndpoint
{
    internal static RouteHandlerBuilder MapDeleteCityEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapDelete("/cities/{id:guid}",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                {
                    await mediator.Send(new DeleteCityCommand(id), ct);
                    return Results.NoContent();
                })
            .WithName("DeleteCity")
            .WithSummary("Soft-delete a city")
            .RequirePermission(PlatformPermissions.Lookups.Manage);
    }
}
