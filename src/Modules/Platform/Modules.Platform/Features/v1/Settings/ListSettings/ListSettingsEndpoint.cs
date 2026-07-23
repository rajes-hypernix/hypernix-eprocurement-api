using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Settings.ListSettings;

public static class ListSettingsEndpoint
{
    internal static RouteHandlerBuilder MapListSettingsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/settings",
                async (IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ListSettingsQuery(), ct)))
            .WithName("ListSettings")
            .WithSummary("List platform settings")
            .RequirePermission(PlatformPermissions.Configuration.View);
    }
}
