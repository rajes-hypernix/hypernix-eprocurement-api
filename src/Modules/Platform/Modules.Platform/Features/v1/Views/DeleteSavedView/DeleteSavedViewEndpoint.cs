using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Views;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Views.DeleteSavedView;

public static class DeleteSavedViewEndpoint
{
    internal static RouteHandlerBuilder MapDeleteSavedViewEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapDelete("/views/{id:guid}",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                {
                    await mediator.Send(new DeleteSavedViewCommand(id), ct);
                    return Results.NoContent();
                })
            .WithName("DeleteSavedView")
            .WithSummary("Delete a saved view (system views cannot be deleted)")
            .RequirePermission(PlatformPermissions.Views.ManageOwn);
    }
}
