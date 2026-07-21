using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.Invoices;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.Invoices.ResolveInvoiceException;

public static class ResolveInvoiceExceptionEndpoint
{
    internal static RouteHandlerBuilder MapResolveInvoiceExceptionEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/invoices/{invoiceId:guid}/resolve-exception",
                async (Guid invoiceId, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new ResolveInvoiceExceptionCommand(invoiceId), ct);
                    return Results.Ok(result);
                })
            .WithName("ResolveInvoiceException")
            .WithSummary("Resolve an invoice exception and return to Submitted status")
            .RequirePermission(ProcurementPermissions.Invoices.ResolveException);
    }
}
