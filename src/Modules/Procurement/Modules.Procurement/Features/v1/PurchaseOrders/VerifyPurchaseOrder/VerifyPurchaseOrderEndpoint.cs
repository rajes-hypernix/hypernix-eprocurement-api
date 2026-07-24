using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.VerifyPurchaseOrder;

public static class VerifyPurchaseOrderEndpoint
{
    internal static RouteHandlerBuilder MapVerifyPurchaseOrderEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/purchase-orders/{poId:guid}/verify",
                async (Guid poId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new VerifyPurchaseOrderCommand(poId), ct)))
            .WithName("VerifyPurchaseOrder")
            .WithSummary("Verify a Draft purchase order, checking required fields are complete")
            .RequirePermission(ProcurementPermissions.PurchaseOrders.Issue);
    }
}
