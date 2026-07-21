using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Evaluation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Evaluation.GetTechnicalEval;

public static class GetTechnicalEvalEndpoint
{
    internal static RouteHandlerBuilder MapGetTechnicalEvalEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/rfqs/{rfqId:guid}/technical-eval",
                (Guid rfqId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetTechnicalEvalQuery(rfqId), ct))
            .WithName("GetTechnicalEval")
            .WithSummary("Get the technical evaluation for an RFQ — vendor identity masked for pure evaluators")
            .RequirePermission(SourcingPermissions.Evaluation.ViewTechnical);
    }
}
