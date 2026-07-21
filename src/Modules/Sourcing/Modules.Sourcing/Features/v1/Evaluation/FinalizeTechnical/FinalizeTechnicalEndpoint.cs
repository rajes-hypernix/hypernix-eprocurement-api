using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Evaluation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Evaluation.FinalizeTechnical;

public static class FinalizeTechnicalEndpoint
{
    internal static RouteHandlerBuilder MapFinalizeTechnicalEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/rfqs/{rfqId:guid}/technical-eval/finalize",
                (Guid rfqId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new FinalizeTechnicalCommand(rfqId), ct))
            .WithName("FinalizeTechnical")
            .WithSummary("Finalize the technical evaluation — requires every submitted bid to be fully scored")
            .RequirePermission(SourcingPermissions.Evaluation.FinalizeTechnical);
    }
}
