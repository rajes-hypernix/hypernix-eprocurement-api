using eProcure.Application.Procurement;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class StatementsController(IStatementService statements) : ControllerBase
{
    [HttpGet("statements")]
    public async Task<ActionResult<IReadOnlyList<StatementSummaryDto>>> List(CancellationToken ct) =>
        Ok(await statements.ListAsync(ct));

    [HttpGet("statements/{vendorId:guid}")]
    public async Task<ActionResult<StatementDetailDto>> Get(Guid vendorId, CancellationToken ct)
    {
        var dto = await statements.GetAsync(vendorId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("my/statement")]
    public async Task<ActionResult<StatementDetailDto>> Mine(CancellationToken ct)
    {
        var dto = await statements.GetMineAsync(ct);
        return dto is null ? NoContent() : Ok(dto);
    }
}
