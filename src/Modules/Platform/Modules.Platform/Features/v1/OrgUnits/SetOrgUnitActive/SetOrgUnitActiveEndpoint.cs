using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.OrgUnits;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.OrgUnits.SetOrgUnitActive;

public static class SetOrgUnitActiveEndpoint
{
    internal static RouteHandlerBuilder MapSetOrgUnitActiveEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/org-units/{id:guid}/active",
                async (Guid id, SetOrgUnitActiveBody body, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new SetOrgUnitActiveCommand(id, body.IsActive), ct)))
            .WithName("SetOrgUnitActive")
            .WithSummary("Activate or deactivate an organisation unit")
            .RequirePermission(PlatformPermissions.Org.Manage);
    }

    private sealed record SetOrgUnitActiveBody(bool IsActive);
}
