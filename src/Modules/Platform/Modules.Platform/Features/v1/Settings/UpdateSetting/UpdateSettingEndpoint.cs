using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Settings.UpdateSetting;

public static class UpdateSettingEndpoint
{
    internal static RouteHandlerBuilder MapUpdateSettingEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/settings/{key}",
                async (string key, UpdateSettingBody body, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new UpdateSettingCommand(key, body.Value), ct);
                    return Results.Ok(result);
                })
            .WithName("UpdateSetting")
            .WithSummary("Update a platform setting value")
            .RequirePermission(PlatformPermissions.Configuration.Manage);
    }

    private sealed record UpdateSettingBody(string Value);
}
