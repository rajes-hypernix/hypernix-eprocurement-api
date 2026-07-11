using eProcure.Api.Auth;
using eProcure.Application.Authorization;
using eProcure.Application.Procurement;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class InvoicesController(IInvoiceService invoices) : ControllerBase
{
    [HttpGet("invoices")]
    [Action(ApiActions.ViewInvoices)]
    public async Task<ActionResult<IReadOnlyList<InvoiceListDto>>> List(CancellationToken ct) =>
        Ok(await invoices.ListAsync(ct));

    [HttpGet("invoices/{id:guid}")]
    [Action(ApiActions.ViewInvoices)]
    public async Task<ActionResult<InvoiceDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var dto = await invoices.GetAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("pos/{poId:guid}/billable")]
    [Action(ApiActions.ViewInvoices)]
    public async Task<ActionResult<InvoiceBillablePlan>> Billable(Guid poId, CancellationToken ct) =>
        Ok(await invoices.GetBillablePlanAsync(poId, ct));

    [HttpPost("pos/{poId:guid}/invoices")]
    [Action(ApiActions.SubmitInvoice)]
    public async Task<ActionResult<InvoiceDetailDto>> Submit(Guid poId, [FromBody] SubmitInvoiceRequest req, CancellationToken ct) =>
        Ok(await invoices.SubmitAsync(poId, req, ct));

    [HttpPost("invoices/{id:guid}/approve")]
    [Action(ApiActions.ApproveInvoice)]
    public async Task<ActionResult<InvoiceDetailDto>> Approve(Guid id, CancellationToken ct) =>
        Ok(await invoices.ApproveAsync(id, ct));

    [HttpPost("invoices/{id:guid}/resolve")]
    [Action(ApiActions.ResolveInvoiceException)]
    public async Task<ActionResult<InvoiceDetailDto>> Resolve(Guid id, CancellationToken ct) =>
        Ok(await invoices.ResolveExceptionAsync(id, ct));
}
