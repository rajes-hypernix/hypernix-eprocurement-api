using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Contracts.v1.Invoices;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Procurement.Features.v1.Invoices.SubmitInvoice;

public static class SubmitInvoiceEndpoint
{
    internal static RouteHandlerBuilder MapSubmitInvoiceEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/invoices",
                async (SubmitInvoiceCommand command, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(command, ct);
                    return Results.Ok(result);
                })
            .WithName("SubmitInvoice")
            .WithSummary("Submit an invoice against a purchase order")
            .RequirePermission(ProcurementPermissions.Invoices.Submit);
    }
}
