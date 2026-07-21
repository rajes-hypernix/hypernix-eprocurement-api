using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.DeclineInvitation;

public static class DeclineInvitationEndpoint
{
    internal static RouteHandlerBuilder MapDeclineInvitationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/rfqs/{rfqId:guid}/decline",
                async (Guid rfqId, DeclineInvitationBody body, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new DeclineInvitationCommand(rfqId, body.ReasonCode, body.Note), ct)))
            .WithName("DeclineRfqInvitation")
            .WithSummary("Vendor declines an RFQ invitation (reason code required)");
    }
}

public sealed record DeclineInvitationBody(string ReasonCode, string? Note);
