using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Views;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Views.RunSavedView;

public static class RunSavedViewEndpoint
{
    internal static RouteHandlerBuilder MapRunSavedViewEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/views/{id:guid}/run",
                async (Guid id, int? page, int? size, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new RunSavedViewQuery(id, page ?? 1, size ?? 50), ct)))
            .WithName("RunSavedView")
            .WithSummary("Execute a saved view's filters/columns and return a page of rows")
            .RequirePermission(PlatformPermissions.Views.View);
    }
}
