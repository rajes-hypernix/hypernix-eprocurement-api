using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Numbering.ListNumberingSchemes;

public static class ListNumberingSchemesEndpoint
{
    internal static RouteHandlerBuilder MapListNumberingSchemesEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/numbering-schemes",
                async (IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ListNumberingSchemesQuery(), ct)))
            .WithName("ListNumberingSchemes")
            .WithSummary("List document numbering schemes")
            .RequirePermission(PlatformPermissions.Configuration.View);
    }
}
