using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Views;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Views.UpdateSavedView;

public static class UpdateSavedViewEndpoint
{
    internal static RouteHandlerBuilder MapUpdateSavedViewEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/views/{id:guid}",
                async (Guid id, SaveViewRequest request, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new UpdateSavedViewCommand(id, request), ct)))
            .WithName("UpdateSavedView")
            .WithSummary("Update a saved view's name/filters/columns")
            .RequirePermission(PlatformPermissions.Views.ManageOwn);
    }
}
