using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.Grns;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.Grns.GetGrnByAsn;

public static class GetGrnByAsnEndpoint
{
    internal static RouteHandlerBuilder MapGetGrnByAsnEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/grns/by-asn/{asnId:guid}",
                async (Guid asnId, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new GetGrnByAsnQuery(asnId), ct);
                    return result is null ? Results.NotFound() : Results.Ok(result);
                })
            .WithName("GetGrnByAsn")
            .WithSummary("Get the GRN for an ASN")
            .RequirePermission(ProcurementPermissions.Deliveries.View);
    }
}
