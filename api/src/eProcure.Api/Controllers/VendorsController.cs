using eProcure.Application;
using eProcure.Application.Audit;
using eProcure.Application.Suppliers;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

[ApiController]
[Route("api/vendors")]
public sealed class VendorsController(IVendorService vendors, IAuditQuery audit) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VendorListItem>>> List(
        [FromQuery] string? q, [FromQuery] string? type, [FromQuery] string? region, CancellationToken ct) =>
        Ok(await vendors.ListAsync(new VendorFilter(q, type, region), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VendorDetail>> Get(Guid id, CancellationToken ct)
    {
        var v = await vendors.GetAsync(id, ct);
        return v is null ? NotFound() : Ok(v);
    }

    [HttpPost]
    public async Task<ActionResult<VendorDetail>> Create([FromBody] CreateVendorRequest req, CancellationToken ct)
    {
        var v = await vendors.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = v.Id }, v);
    }

    /// <summary>Manual New-Vendor entry — straight to the master, no onboarding approval (C1/C2).</summary>
    [HttpPost("manual")]
    public async Task<ActionResult<ManualVendorResult>> CreateManual([FromBody] CreateManualVendorRequest req, CancellationToken ct) =>
        Ok(await vendors.CreateManualAsync(req, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<VendorDetail>> Update(Guid id, [FromBody] UpdateVendorRequest req, CancellationToken ct) =>
        Ok(await vendors.UpdateAsync(id, req, ct));

    [HttpPut("{id:guid}/categories")]
    public async Task<ActionResult<VendorDetail>> SetCategories(Guid id, [FromBody] SetCategoriesRequest req, CancellationToken ct) =>
        Ok(await vendors.SetCategoriesAsync(id, req, ct));

    [HttpPost("{id:guid}/toggle-status")]
    public async Task<ActionResult<VendorDetail>> ToggleStatus(Guid id, CancellationToken ct) =>
        Ok(await vendors.ToggleStatusAsync(id, ct));

    [HttpGet("{id:guid}/audit")]
    public async Task<ActionResult<IReadOnlyList<AuditEntryDto>>> Audit(Guid id, CancellationToken ct)
    {
        var v = await vendors.GetAsync(id, ct);
        if (v is null) return NotFound();
        return Ok(await audit.ListForAsync("Vendor", v.Code, ct));
    }
}
