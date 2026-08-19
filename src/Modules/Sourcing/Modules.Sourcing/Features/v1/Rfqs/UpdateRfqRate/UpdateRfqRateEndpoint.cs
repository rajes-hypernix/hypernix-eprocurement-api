using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.UpdateRfqRate;

public static class UpdateRfqRateEndpoint
{
    internal static RouteHandlerBuilder MapUpdateRfqRateEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/rfqs/{rfqId:guid}/update-rate",
                async (Guid rfqId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new UpdateRfqRateCommand(rfqId), ct)))
            .WithName("UpdateRfqRate")
            .WithSummary("Re-snapshot the current Platform FX rate onto a draft RFQ")
            .RequirePermission(SourcingPermissions.Rfqs.ManageDraft);
    }
}
