using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Evaluation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Evaluation.OpenCommercialEnvelope;

public static class OpenCommercialEnvelopeEndpoint
{
    internal static RouteHandlerBuilder MapOpenCommercialEnvelopeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/rfqs/{rfqId:guid}/commercial-envelope/open",
                (Guid rfqId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new OpenCommercialEnvelopeCommand(rfqId), ct))
            .WithName("OpenCommercialEnvelope")
            .WithSummary("Open the sealed commercial envelope for an RFQ")
            .RequirePermission(SourcingPermissions.Evaluation.OpenCommercial);
    }
}
