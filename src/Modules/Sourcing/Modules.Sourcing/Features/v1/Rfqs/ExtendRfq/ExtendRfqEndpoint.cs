using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.ExtendRfq;

public static class ExtendRfqEndpoint
{
    internal static RouteHandlerBuilder MapExtendRfqEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/rfqs/{rfqId:guid}/extend",
                async (Guid rfqId, ExtendRfqBody body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(new ExtendRfqCommand(rfqId, body.NewClosesUtc, body.ReasonCode, body.Note), ct));
                })
            .WithName("ExtendRfq")
            .WithSummary("Extend an RFQ's close date")
            .RequirePermission(SourcingPermissions.Rfqs.Extend);
    }
}

public sealed record ExtendRfqBody(DateTime NewClosesUtc, string? ReasonCode, string? Note);
