using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.Asns;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.Asns.GetAsn;

public static class GetAsnEndpoint
{
    internal static RouteHandlerBuilder MapGetAsnEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/asns/{asnId:guid}",
                async (Guid asnId, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new GetAsnQuery(asnId), ct);
                    return result is null ? Results.NotFound() : Results.Ok(result);
                })
            .WithName("GetAsn")
            .WithSummary("Get an ASN by ID")
            .RequirePermission(ProcurementPermissions.Deliveries.View);
    }
}
