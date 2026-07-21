using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.Grns;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.Grns.ReceiveAsn;

public static class ReceiveAsnEndpoint
{
    internal static RouteHandlerBuilder MapReceiveAsnEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/grns/receive",
                async (ReceiveAsnCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(command, ct);
                    return Results.Ok(result);
                })
            .WithName("ReceiveAsn")
            .WithSummary("Receive an ASN and create a goods receipt note")
            .RequirePermission(ProcurementPermissions.Deliveries.Receive);
    }
}
