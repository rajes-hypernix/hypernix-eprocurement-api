using FSH.Modules.Sourcing.Contracts.v1.Clarifications;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Clarifications.GetClarificationThread;

public static class GetClarificationThreadEndpoint
{
    internal static RouteHandlerBuilder MapGetClarificationThreadEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/clarifications/threads/{scope}/{vendorId:guid}",
                (string scope, Guid vendorId, IMediator mediator, CancellationToken ct) =>
                    mediator.Send(new GetClarificationThreadQuery(scope, vendorId), ct))
            .WithName("GetClarificationThread")
            .WithSummary("Get one clarification thread — marks the counterparty's messages read for the caller's side.");
    }
}
