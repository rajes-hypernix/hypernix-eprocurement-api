using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Countries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Countries.DeleteCountry;

public static class DeleteCountryEndpoint
{
    internal static RouteHandlerBuilder MapDeleteCountryEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapDelete("/countries/{id:guid}",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                {
                    await mediator.Send(new DeleteCountryCommand(id), ct);
                    return Results.NoContent();
                })
            .WithName("DeleteCountry")
            .WithSummary("Soft-delete a country")
            .RequirePermission(PlatformPermissions.Lookups.Manage);
    }
}
