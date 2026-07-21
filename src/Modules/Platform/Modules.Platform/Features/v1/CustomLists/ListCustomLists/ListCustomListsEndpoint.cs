using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.CustomLists;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.CustomLists.ListCustomLists;

public static class ListCustomListsEndpoint
{
    internal static RouteHandlerBuilder MapListCustomListsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/custom-lists",
                async (bool? activeOnly, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new ListCustomListsQuery(activeOnly ?? true), ct);
                    return Results.Ok(result);
                })
            .WithName("ListCustomLists")
            .WithSummary("List custom list definitions")
            .RequirePermission(PlatformPermissions.CustomLists.View);
    }
}
