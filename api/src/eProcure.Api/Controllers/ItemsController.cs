using eProcure.Api.Auth;
using eProcure.Application.Authorization;
using eProcure.Application.Procurement;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

/// <summary>CFH-T4: the Item Master — a small reusable lookup source. Reads are open to every PR
/// raiser (the entry-form picker); writes are admin-only.</summary>
[ApiController]
[Route("api/items")]
public sealed class ItemsController(IItemService items) : ControllerBase
{
    [HttpGet]
    [Action(ApiActions.ViewItems)]
    public async Task<ActionResult<IReadOnlyList<ItemDto>>> List([FromQuery] bool activeOnly, CancellationToken ct) =>
        Ok(await items.ListAsync(activeOnly, ct));

    [HttpGet("{id:guid}")]
    [Action(ApiActions.ViewItems)]
    public async Task<ActionResult<ItemDto>> Get(Guid id, CancellationToken ct) =>
        await items.GetAsync(id, ct) is { } item ? Ok(item) : NotFound();

    [HttpPost]
    [Action(ApiActions.ManageItems)]
    public async Task<ActionResult<ItemDto>> Create(SaveItemRequest req, CancellationToken ct) =>
        Ok(await items.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [Action(ApiActions.ManageItems)]
    public async Task<ActionResult<ItemDto>> Update(Guid id, [FromBody] SaveItemRequest req, CancellationToken ct) =>
        Ok(await items.UpdateAsync(id, req, ct));

    [HttpPost("{id:guid}/active")]
    [Action(ApiActions.ManageItems)]
    public async Task<ActionResult<ItemDto>> SetActive(Guid id, [FromBody] bool active, CancellationToken ct) =>
        Ok(await items.SetActiveAsync(id, active, ct));

    [HttpDelete("{id:guid}")]
    [Action(ApiActions.ManageItems)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await items.DeleteAsync(id, ct);
        return NoContent();
    }
}
