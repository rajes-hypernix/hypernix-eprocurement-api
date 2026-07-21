using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Evaluation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Evaluation.OpenTechnicalEnvelope;

public static class OpenTechnicalEnvelopeEndpoint
{
    internal static RouteHandlerBuilder MapOpenTechnicalEnvelopeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/rfqs/{rfqId:guid}/technical-envelope/open",
                (Guid rfqId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new OpenTechnicalEnvelopeCommand(rfqId), ct))
            .WithName("OpenTechnicalEnvelope")
            .WithSummary("Open the sealed technical envelope for a Dual-envelope RFQ")
            .RequirePermission(SourcingPermissions.Evaluation.OpenTechnical);
    }
}
