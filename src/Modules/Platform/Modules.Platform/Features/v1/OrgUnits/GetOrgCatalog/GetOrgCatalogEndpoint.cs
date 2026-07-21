using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.OrgUnits;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.OrgUnits.GetOrgCatalog;

public static class GetOrgCatalogEndpoint
{
    internal static RouteHandlerBuilder MapGetOrgCatalogEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/org-catalog",
                async (bool? activeOnly, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new GetOrgCatalogQuery(activeOnly ?? true), ct)))
            .WithName("GetOrgCatalog")
            .WithSummary("Organisation unit catalog for PR pickers")
            .RequirePermission(PlatformPermissions.Org.View);
    }
}
