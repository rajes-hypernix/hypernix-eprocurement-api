using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.OrgUnits;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.OrgUnits.ListOrgUnits;

public static class ListOrgUnitsEndpoint
{
    internal static RouteHandlerBuilder MapListOrgUnitsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/org-units",
                async (string? type, bool? activeOnly, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ListOrgUnitsQuery(type, activeOnly ?? true), ct)))
            .WithName("ListOrgUnits")
            .WithSummary("List organisation units")
            .RequirePermission(PlatformPermissions.Org.View);
    }
}
