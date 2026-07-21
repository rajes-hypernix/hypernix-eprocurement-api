using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.Invoices;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.Invoices.ApproveInvoice;

public static class ApproveInvoiceEndpoint
{
    internal static RouteHandlerBuilder MapApproveInvoiceEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/invoices/{invoiceId:guid}/approve",
                async (Guid invoiceId, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new ApproveInvoiceCommand(invoiceId), ct);
                    return Results.Ok(result);
                })
            .WithName("ApproveInvoice")
            .WithSummary("Approve an invoice for payment")
            .RequirePermission(ProcurementPermissions.Invoices.Approve);
    }
}
