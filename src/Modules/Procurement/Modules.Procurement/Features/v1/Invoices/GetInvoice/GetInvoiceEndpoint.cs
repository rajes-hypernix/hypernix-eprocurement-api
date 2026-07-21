using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.Invoices;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.Invoices.GetInvoice;

public static class GetInvoiceEndpoint
{
    internal static RouteHandlerBuilder MapGetInvoiceEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/invoices/{invoiceId:guid}",
                async (Guid invoiceId, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new GetInvoiceQuery(invoiceId), ct);
                    return result is null ? Results.NoContent() : Results.Ok(result);
                })
            .WithName("GetInvoice")
            .WithSummary("Get an invoice by ID")
            .RequirePermission(ProcurementPermissions.Invoices.View);
    }
}
