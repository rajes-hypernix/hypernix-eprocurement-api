using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Awards;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Awards.GetAwardEligibility;

public static class GetAwardEligibilityEndpoint
{
    internal static RouteHandlerBuilder MapGetAwardEligibilityEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/rfqs/{rfqId:guid}/award-eligibility",
                (Guid rfqId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetAwardEligibilityQuery(rfqId), ct))
            .WithName("GetAwardEligibility")
            .WithSummary("Get each invited vendor's award eligibility — identity/pricing masked until commercial reveal")
            .RequirePermission(SourcingPermissions.Award.View);
    }
}
