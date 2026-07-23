using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Views;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Views.ShareSavedView;

public static class ShareSavedViewEndpoint
{
    internal static RouteHandlerBuilder MapShareSavedViewEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/views/{id:guid}/share",
                async (Guid id, ShareSavedViewBody body, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ShareSavedViewCommand(id, body.IsShared), ct)))
            .WithName("ShareSavedView")
            .WithSummary("Publish or unpublish a saved view for everyone")
            .RequirePermission(PlatformPermissions.Views.ManageShared);
    }

    private sealed record ShareSavedViewBody(bool IsShared);
}
