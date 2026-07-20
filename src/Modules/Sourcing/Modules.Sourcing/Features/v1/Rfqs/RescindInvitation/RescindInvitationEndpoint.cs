using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.RescindInvitation;

public static class RescindInvitationEndpoint
{
    internal static RouteHandlerBuilder MapRescindInvitationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/rfqs/{rfqId:guid}/invitations/{vendorId:guid}/rescind",
                async (Guid rfqId, Guid vendorId, RescindInvitationBody body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(new RescindInvitationCommand(rfqId, vendorId, body.ReasonCode, body.Note), ct));
                })
            .WithName("RescindRfqInvitation")
            .WithSummary("Rescind a vendor's RFQ invitation")
            .RequirePermission(SourcingPermissions.Rfqs.Rescind);
    }
}

public sealed record RescindInvitationBody(string ReasonCode, string? Note);
