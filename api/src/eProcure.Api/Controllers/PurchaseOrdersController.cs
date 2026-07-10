using eProcure.Application.Audit;
using eProcure.Application.Procurement;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

[ApiController]
[Route("api/pos")]
public sealed class PurchaseOrdersController(IPoService pos, IAuditQuery audit) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PoListItem>>> List(CancellationToken ct) =>
        Ok(await pos.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PoDetail>> Get(Guid id, CancellationToken ct)
    {
        var po = await pos.GetAsync(id, ct);
        return po is null ? NotFound() : Ok(po);
    }

    [HttpGet("{id:guid}/audit")]
    public async Task<ActionResult<IReadOnlyList<AuditEntryDto>>> Audit(Guid id, CancellationToken ct)
    {
        var po = await pos.GetAsync(id, ct);
        return po is null ? NotFound() : Ok(await audit.ListForAsync("Po", po.Code, ct));
    }

    [HttpPost("{id:guid}/issue")]
    public async Task<ActionResult<PoDetail>> Issue(Guid id, CancellationToken ct) =>
        Ok(await pos.IssueAsync(id, ct));

    [HttpPost("{id:guid}/acknowledge")]
    public async Task<ActionResult<PoDetail>> Acknowledge(Guid id, CancellationToken ct) =>
        Ok(await pos.AcknowledgeAsync(id, ct));
}
