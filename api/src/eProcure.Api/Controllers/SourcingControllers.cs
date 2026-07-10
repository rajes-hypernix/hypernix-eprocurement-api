using eProcure.Application.Sourcing;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

[ApiController]
[Route("api/requisitions")]
public sealed class RequisitionsController(IRequisitionService reqs) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RequisitionDto>>> List(CancellationToken ct) =>
        Ok(await reqs.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RequisitionDto>> Get(Guid id, CancellationToken ct) =>
        await reqs.GetAsync(id, ct) is { } pr ? Ok(pr) : NotFound();

    [HttpPost]
    public async Task<ActionResult<RequisitionDto>> Create([FromQuery] bool submit, SavePrRequest req, CancellationToken ct) =>
        Ok(await reqs.CreateAsync(req, submit, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RequisitionDto>> Update(Guid id, SavePrRequest req, CancellationToken ct) =>
        Ok(await reqs.UpdateAsync(id, req, ct));

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<RequisitionDto>> Submit(Guid id, CancellationToken ct) =>
        Ok(await reqs.SubmitAsync(id, ct));

    [HttpPost("{id:guid}/lines/{lineId:guid}/cancel")]
    public async Task<ActionResult<RequisitionDto>> CancelLine(Guid id, Guid lineId, ReasonRequest body, CancellationToken ct) =>
        Ok(await reqs.CancelLineAsync(id, lineId, body.Reason, ct));

    [HttpPost("{id:guid}/lines/{lineId:guid}/release")]
    public async Task<ActionResult<RequisitionDto>> ReleaseLine(Guid id, Guid lineId, ReasonRequest body, CancellationToken ct) =>
        Ok(await reqs.ReleaseLineAsync(id, lineId, body.Reason, ct));

    [HttpPost("{id:guid}/lines/{lineId:guid}/reopen")]
    public async Task<ActionResult<RequisitionDto>> ReopenLine(Guid id, Guid lineId, CancellationToken ct) =>
        Ok(await reqs.ReopenLineAsync(id, lineId, ct));

    [HttpPost("{id:guid}/lines/{lineId:guid}/reserve")]
    public async Task<ActionResult<RequisitionDto>> ReserveLine(Guid id, Guid lineId, CancellationToken ct) =>
        Ok(await reqs.ReserveLineAsync(id, lineId, ct));

    [HttpPost("{id:guid}/lines/{lineId:guid}/unreserve")]
    public async Task<ActionResult<RequisitionDto>> UnreserveLine(Guid id, Guid lineId, CancellationToken ct) =>
        Ok(await reqs.UnreserveLineAsync(id, lineId, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<RequisitionDto>> Cancel(Guid id, ReasonRequest body, CancellationToken ct) =>
        Ok(await reqs.CancelPrAsync(id, body.Reason, ct));
}

[ApiController]
[Route("api/rfqs")]
public sealed class RfqsController(IRfqService rfqs) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RfqListItem>>> List(CancellationToken ct) =>
        Ok(await rfqs.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RfqDetail>> Get(Guid id, CancellationToken ct)
    {
        var r = await rfqs.GetAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost]
    public async Task<ActionResult<RfqDetail>> CreateDraft([FromBody] CreateRfqDraftRequest req, CancellationToken ct)
    {
        var r = await rfqs.CreateDraftAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = r.Id }, r);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RfqDetail>> UpdateDraft(Guid id, [FromBody] UpdateRfqDraftRequest req, CancellationToken ct) =>
        Ok(await rfqs.UpdateDraftAsync(id, req, ct));

    [HttpPost("{id:guid}/release")]
    public async Task<ActionResult<RfqDetail>> Release(Guid id, CancellationToken ct) =>
        Ok(await rfqs.ReleaseAsync(id, ct));

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<RfqDetail>> Close(Guid id, CancellationToken ct) =>
        Ok(await rfqs.CloseAsync(id, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<RfqDetail>> Cancel(Guid id, CancellationToken ct) =>
        Ok(await rfqs.CancelAsync(id, ct));

    // ---- Buyer governance (Slice I) ----
    [HttpPost("{id:guid}/invitations")]
    public async Task<ActionResult<RfqDetail>> Invite(Guid id, [FromBody] InviteVendorRequest req, CancellationToken ct) =>
        Ok(await rfqs.InviteVendorAsync(id, req, ct));

    [HttpPost("{id:guid}/invitations/{vendorId:guid}/rescind")]
    public async Task<ActionResult<RfqDetail>> Rescind(Guid id, Guid vendorId, [FromBody] RescindInvitationRequest req, CancellationToken ct) =>
        Ok(await rfqs.RescindInvitationAsync(id, vendorId, req, ct));

    [HttpPost("{id:guid}/extend")]
    public async Task<ActionResult<RfqDetail>> Extend(Guid id, [FromBody] ExtendRfqRequest req, CancellationToken ct) =>
        Ok(await rfqs.ExtendAsync(id, req, ct));
}

/// <summary>Vendor-side RFQ invitation actions (RFQ-LIFECYCLE-ADDENDUM §6). Scoped to the caller's own
/// vendor via ICurrentUser inside the service; mirrors the existing vendor bid route convention.</summary>
[ApiController]
[Route("api/my/rfqs/{rfqId:guid}")]
public sealed class MyRfqActionsController(IRfqVendorService svc) : ControllerBase
{
    [HttpPost("decline")]
    public async Task<IActionResult> Decline(Guid rfqId, [FromBody] DeclineInvitationRequest req, CancellationToken ct)
    {
        await svc.DeclineAsync(rfqId, req, ct);
        return NoContent();
    }

    [HttpPost("intend")]
    public async Task<IActionResult> Intend(Guid rfqId, CancellationToken ct)
    {
        await svc.IntendAsync(rfqId, ct);
        return NoContent();
    }

    [HttpPost("withdraw-bid")]
    public async Task<IActionResult> WithdrawBid(Guid rfqId, CancellationToken ct)
    {
        await svc.WithdrawBidAsync(rfqId, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/forms")]
public sealed class FormsController(IFormService forms) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FormTemplateDto>>> List(CancellationToken ct) =>
        Ok(await forms.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FormTemplateDto>> Get(Guid id, CancellationToken ct)
    {
        var f = await forms.GetAsync(id, ct);
        return f is null ? NotFound() : Ok(f);
    }

    [HttpPost]
    public async Task<ActionResult<FormTemplateDto>> Create([FromBody] SaveFormTemplateRequest req, CancellationToken ct) =>
        Ok(await forms.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FormTemplateDto>> Update(Guid id, [FromBody] SaveFormTemplateRequest req, CancellationToken ct) =>
        Ok(await forms.UpdateAsync(id, req, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await forms.DeleteAsync(id, ct);
        return NoContent();
    }
}
