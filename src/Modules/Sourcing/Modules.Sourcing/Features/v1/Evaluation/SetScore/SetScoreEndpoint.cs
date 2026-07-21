using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Evaluation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Evaluation.SetScore;

public static class SetScoreEndpoint
{
    internal static RouteHandlerBuilder MapSetScoreEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/rfqs/{rfqId:guid}/technical-eval/scores",
                async (Guid rfqId, SetScoreBody body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(new SetScoreCommand(rfqId, body.VendorId, body.Criterion, body.Score), ct));
                })
            .WithName("SetTechnicalScore")
            .WithSummary("Upsert one evaluator's score for one criterion on one vendor's bid")
            .RequirePermission(SourcingPermissions.Evaluation.Score);
    }
}

public sealed record SetScoreBody(Guid VendorId, string Criterion, int Score);
