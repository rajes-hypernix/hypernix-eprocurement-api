using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.CreateFromRequisition;

public static class CreatePurchaseOrderFromRequisitionEndpoint
{
    internal static RouteHandlerBuilder MapCreatePurchaseOrderFromRequisitionEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/purchase-orders/from-requisition",
                async (CreatePurchaseOrderFromRequisitionCommand command, IMediator mediator, CancellationToken ct) =>
                    Results.Ok(await mediator.Send(command, ct)))
            .WithName("CreatePurchaseOrderFromRequisition")
            .WithSummary("Create a purchase order directly from selected requisition lines")
            .RequirePermission(ProcurementPermissions.PurchaseOrders.CreateFromRequisition);
    }
}
