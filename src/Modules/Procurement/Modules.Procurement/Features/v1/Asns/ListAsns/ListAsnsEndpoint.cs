using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.Asns;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.Asns.ListAsns;

public static class ListAsnsEndpoint
{
    internal static RouteHandlerBuilder MapListAsnsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/asns",
                async (Guid? poId, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new ListAsnsQuery(poId), ct);
                    return Results.Ok(result);
                })
            .WithName("ListAsns")
            .WithSummary("List ASNs (optional poId filter)")
            .RequirePermission(ProcurementPermissions.Deliveries.View);
    }
}
