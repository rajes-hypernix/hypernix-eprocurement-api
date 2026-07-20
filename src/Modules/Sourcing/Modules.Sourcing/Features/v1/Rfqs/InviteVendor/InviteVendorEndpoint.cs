using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.InviteVendor;

public static class InviteVendorEndpoint
{
    internal static RouteHandlerBuilder MapInviteVendorEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/rfqs/{rfqId:guid}/invitations",
                async (Guid rfqId, InviteVendorBody body, IMediator mediator, CancellationToken ct) =>
                {
                    ArgumentNullException.ThrowIfNull(body);
                    return Results.Ok(await mediator.Send(new InviteVendorCommand(rfqId, body.VendorId), ct));
                })
            .WithName("InviteVendorToRfq")
            .WithSummary("Invite a vendor to an RFQ")
            .RequirePermission(SourcingPermissions.Rfqs.Invite);
    }
}

public sealed record InviteVendorBody(Guid VendorId);
