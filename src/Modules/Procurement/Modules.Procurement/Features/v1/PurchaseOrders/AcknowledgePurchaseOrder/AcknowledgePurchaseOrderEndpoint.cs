using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.AcknowledgePurchaseOrder;

public static class AcknowledgePurchaseOrderEndpoint
{
    internal static RouteHandlerBuilder MapAcknowledgePurchaseOrderEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/purchase-orders/{poId:guid}/acknowledge",
                async (Guid poId, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new AcknowledgePurchaseOrderCommand(poId), ct);
                    return Results.Ok(result);
                })
            .WithName("AcknowledgePurchaseOrder")
            .WithSummary("Vendor acknowledges receipt of a purchase order")
            .RequirePermission(ProcurementPermissions.PurchaseOrders.Acknowledge);
    }
}
