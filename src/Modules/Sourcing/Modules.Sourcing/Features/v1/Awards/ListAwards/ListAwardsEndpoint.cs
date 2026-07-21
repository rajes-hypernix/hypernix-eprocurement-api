using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Awards;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Awards.ListAwards;

public static class ListAwardsEndpoint
{
    internal static RouteHandlerBuilder MapListAwardsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/awards",
                (IMediator mediator, CancellationToken ct) => mediator.Send(new ListAwardsQuery(), ct))
            .WithName("ListAwards")
            .WithSummary("List all awards across RFQs")
            .RequirePermission(SourcingPermissions.Award.View);
    }
}
