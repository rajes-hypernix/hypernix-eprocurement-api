using FSH.Modules.Sourcing.Contracts.v1.Clarifications;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Clarifications.SendClarification;

public static class SendClarificationEndpoint
{
    internal static RouteHandlerBuilder MapSendClarificationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/clarifications",
                async (SendClarificationBody body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(new SendClarificationCommand(body.Scope, body.VendorId, body.Body, body.Published), ct));
                })
            .WithName("SendClarification")
            .WithSummary("Send a clarification message — a buyer publishing to a non-general scope fans out to every live invited vendor.");
    }
}

public sealed record SendClarificationBody(string Scope, Guid? VendorId, string Body, bool Published);
