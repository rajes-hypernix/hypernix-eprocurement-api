using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.GetPurchaseOrder;

public static class GetPurchaseOrderEndpoint
{
    internal static RouteHandlerBuilder MapGetPurchaseOrderEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/purchase-orders/{poId:guid}",
                async (Guid poId, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new GetPurchaseOrderQuery(poId), ct);
                    return result is null ? Results.NotFound() : Results.Ok(result);
                })
            .WithName("GetPurchaseOrder")
            .WithSummary("Get a purchase order by ID")
            .RequirePermission(ProcurementPermissions.PurchaseOrders.View);
    }
}
