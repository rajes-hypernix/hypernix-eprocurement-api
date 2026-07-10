using eProcure.Application.Procurement;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class DeliveriesController(IDeliveryService deliveries) : ControllerBase
{
    [HttpGet("asns")]
    public async Task<ActionResult<IReadOnlyList<AsnListDto>>> List(CancellationToken ct) =>
        Ok(await deliveries.ListAsync(ct));

    [HttpGet("asns/{asnId:guid}")]
    public async Task<ActionResult<AsnDetailDto>> Get(Guid asnId, CancellationToken ct)
    {
        var dto = await deliveries.GetAsync(asnId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("asns/{asnId:guid}/grn")]
    public async Task<ActionResult<GrnDetailDto>> Grn(Guid asnId, CancellationToken ct)
    {
        var dto = await deliveries.GetGrnForAsnAsync(asnId, ct);
        return dto is null ? NoContent() : Ok(dto);
    }

    [HttpPost("asns/{asnId:guid}/receive")]
    public async Task<ActionResult<GrnDetailDto>> Receive(Guid asnId, [FromBody] ReceiveRequest req, CancellationToken ct) =>
        Ok(await deliveries.ReceiveAsync(asnId, req, ct));

    [HttpGet("pos/{poId:guid}/ship-plan")]
    public async Task<ActionResult<ShipPlanDto>> ShipPlan(Guid poId, CancellationToken ct) =>
        Ok(await deliveries.GetShipPlanAsync(poId, ct));

    [HttpPost("pos/{poId:guid}/asns")]
    public async Task<ActionResult<AsnDetailDto>> CreateAsn(Guid poId, [FromBody] CreateAsnRequest req, CancellationToken ct) =>
        Ok(await deliveries.CreateAsnAsync(poId, req, ct));
}
