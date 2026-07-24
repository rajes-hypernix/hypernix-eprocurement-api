using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.UpdatePurchaseOrderLine;

public static class UpdatePurchaseOrderLineEndpoint
{
    internal static RouteHandlerBuilder MapUpdatePurchaseOrderLineEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/purchase-orders/{poId:guid}/lines/{lineId:guid}",
                async (Guid poId, Guid lineId, UpdatePurchaseOrderLineRequest body, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(
                        new UpdatePurchaseOrderLineCommand(poId, lineId, body.UnitPrice, body.PriceConfirmed), ct)))
            .WithName("UpdatePurchaseOrderLine")
            .WithSummary("Edit a Draft purchase order line's price and/or confirm it")
            .RequirePermission(ProcurementPermissions.PurchaseOrders.Issue);
    }
}

public sealed record UpdatePurchaseOrderLineRequest(decimal? UnitPrice, bool PriceConfirmed);
