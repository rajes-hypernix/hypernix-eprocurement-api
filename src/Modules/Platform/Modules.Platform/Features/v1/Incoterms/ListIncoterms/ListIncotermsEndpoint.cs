using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Platform.Features.v1.Incoterms.ListIncoterms;

public static class ListIncotermsEndpoint
{
    internal static RouteHandlerBuilder MapListIncotermsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/incoterms",
                async (bool? activeOnly, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ListIncotermsQuery(activeOnly ?? true), ct)))
            .WithName("ListIncoterms")
            .WithSummary("List incoterms")
            .RequirePermission(PlatformPermissions.Configuration.View);
    }
}
