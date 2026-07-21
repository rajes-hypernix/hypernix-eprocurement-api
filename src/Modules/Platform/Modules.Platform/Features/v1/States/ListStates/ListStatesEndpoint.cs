using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.States;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.States.ListStates;

public static class ListStatesEndpoint
{
    internal static RouteHandlerBuilder MapListStatesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/countries/{countryId:guid}/states",
                async (Guid countryId, bool? activeOnly, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new ListStatesQuery(countryId, activeOnly ?? true), ct);
                    return Results.Ok(result);
                })
            .WithName("ListStates")
            .WithSummary("List states for a country")
            .RequirePermission(PlatformPermissions.Lookups.View);
    }
}
