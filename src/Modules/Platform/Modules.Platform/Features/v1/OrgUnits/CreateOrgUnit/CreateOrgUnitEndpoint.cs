using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.OrgUnits;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.OrgUnits.CreateOrgUnit;

public static class CreateOrgUnitEndpoint
{
    internal static RouteHandlerBuilder MapCreateOrgUnitEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/org-units",
                async (CreateOrgUnitCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("CreateOrgUnit")
            .WithSummary("Create an organisation unit")
            .RequirePermission(PlatformPermissions.Org.Manage);
    }
}
