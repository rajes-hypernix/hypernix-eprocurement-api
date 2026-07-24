using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.ReopenPurchaseOrderDraft;

public static class ReopenPurchaseOrderDraftEndpoint
{
    internal static RouteHandlerBuilder MapReopenPurchaseOrderDraftEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/purchase-orders/{poId:guid}/reopen-draft",
                async (Guid poId, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(new ReopenPurchaseOrderDraftCommand(poId), ct)))
            .WithName("ReopenPurchaseOrderDraft")
            .WithSummary("Reopen a Verified purchase order back to Draft for editing")
            .RequirePermission(ProcurementPermissions.PurchaseOrders.Issue);
    }
}
