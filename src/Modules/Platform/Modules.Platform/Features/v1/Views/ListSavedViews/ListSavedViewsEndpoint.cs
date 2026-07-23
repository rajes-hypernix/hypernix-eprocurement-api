using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Views;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Views.ListSavedViews;

public static class ListSavedViewsEndpoint
{
    internal static RouteHandlerBuilder MapListSavedViewsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/views",
                async (string? recordType, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ListSavedViewsQuery(recordType), ct)))
            .WithName("ListSavedViews")
            .WithSummary("List saved views visible to the caller (system + shared + own)")
            .RequirePermission(PlatformPermissions.Views.View);
    }
}
