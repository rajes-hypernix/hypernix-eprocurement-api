using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Awards;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Awards.ApproveAward;

public static class ApproveAwardEndpoint
{
    internal static RouteHandlerBuilder MapApproveAwardEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/rfqs/{rfqId:guid}/award/approve",
                (Guid rfqId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new ApproveAwardCommand(rfqId), ct))
            .WithName("ApproveAward")
            .WithSummary("Approve an award — Delegation-of-Authority gate, segregation of duties enforced (approver != submitter)")
            .RequirePermission(SourcingPermissions.Award.Approve);
    }
}
