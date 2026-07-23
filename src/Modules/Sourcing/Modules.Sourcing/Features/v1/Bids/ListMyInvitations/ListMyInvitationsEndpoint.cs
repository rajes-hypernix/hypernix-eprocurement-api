using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Bids;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Bids.ListMyInvitations;

public static class ListMyInvitationsEndpoint
{
    internal static RouteHandlerBuilder MapListMyInvitationsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/my/rfqs",
                (IMediator mediator, CancellationToken ct) => mediator.Send(new ListMyInvitationsQuery(), ct))
            .WithName("ListMyInvitations")
            .WithSummary("List the RFQs the calling vendor has been invited to — vendor-portal only, scoped by the caller's vendorId claim.")
            .RequirePermission(SourcingPermissions.Bids.ViewMine);
    }
}
