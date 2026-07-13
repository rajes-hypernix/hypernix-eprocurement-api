using eProcure.Api.Auth;
using eProcure.Application.Authorization;
using eProcure.Application.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

/// <summary>Custom Lists (NetSuite-style conformed-dimension value sets) + admin maintenance.</summary>
[ApiController]
[Route("api/custom-lists")]
public sealed class CustomListsController(ICustomListService lists) : ControllerBase
{
    [HttpGet]
    [Action(ApiActions.ViewCustomLists)]
    public async Task<ActionResult<IReadOnlyList<CustomListDto>>> List(CancellationToken ct) =>
        Ok(await lists.ListAsync(ct));

    [HttpGet("{code}")]
    [Action(ApiActions.ViewCustomLists)]
    public async Task<ActionResult<CustomListDto>> Get(string code, CancellationToken ct) =>
        await lists.GetAsync(code, ct) is { } l ? Ok(l) : NotFound();

    [HttpPost]
    [Action(ApiActions.ManageCustomLists)]
    public async Task<ActionResult<CustomListDto>> Create(CreateCustomListRequest req, CancellationToken ct) =>
        Ok(await lists.CreateListAsync(req, ct));

    [HttpPut("{code}")]
    [Action(ApiActions.ManageCustomLists)]
    public async Task<ActionResult<CustomListDto>> UpdateList(string code, [FromBody] UpdateCustomListRequest req, CancellationToken ct) =>
        Ok(await lists.UpdateListAsync(code, req, ct));

    [HttpPost("{code}/active")]
    [Action(ApiActions.ManageCustomLists)]
    public async Task<ActionResult<CustomListDto>> SetListActive(string code, [FromBody] bool active, CancellationToken ct) =>
        Ok(await lists.SetListActiveAsync(code, active, ct));

    /// <summary>CF1-T2: 200 + the deactivated list when the guard blocked removal; 204 when gone.</summary>
    [HttpDelete("{code}")]
    [Action(ApiActions.ManageCustomLists)]
    public async Task<IActionResult> DeleteList(string code, CancellationToken ct)
    {
        if (await lists.DeleteListAsync(code, ct) is { } deactivated) return Ok(deactivated);
        return NoContent();
    }

    [HttpPost("{code}/values")]
    [Action(ApiActions.ManageCustomLists)]
    public async Task<ActionResult<CustomListValueDto>> AddValue(string code, AddCustomListValueRequest req, CancellationToken ct) =>
        Ok(await lists.AddValueAsync(code, req, ct));

    [HttpPut("values/{valueId:guid}")]
    [Action(ApiActions.ManageCustomLists)]
    public async Task<ActionResult<CustomListValueDto>> UpdateValue(Guid valueId, UpdateCustomListValueRequest req, CancellationToken ct) =>
        Ok(await lists.UpdateValueAsync(valueId, req, ct));

    // CF-FIX3-T4: value impact report (ADMIN-scoped) + governed purge (A73).
    [HttpGet("values/{valueId:guid}/references")]
    [Action(ApiActions.ManageCustomLists)]
    public async Task<ActionResult<eProcure.Application.CustomFields.ImpactReportDto>> ValueReferences(Guid valueId, CancellationToken ct) =>
        Ok(await lists.GetValueReferencesAsync(valueId, ct));

    [HttpPost("values/{valueId:guid}/purge")]
    [Action(ApiActions.PurgeCustomFieldHistory)]
    public async Task<IActionResult> PurgeValue(Guid valueId, CancellationToken ct)
    {
        await lists.PurgeValueAsync(valueId, ct);
        return NoContent();
    }

    [HttpDelete("values/{valueId:guid}")]
    [Action(ApiActions.ManageCustomLists)]
    public async Task<IActionResult> DeleteValue(Guid valueId, CancellationToken ct)
    {
        // A2F-T3: 200 + the inactive value when the in-use guard deactivated it; 204 when gone.
        if (await lists.DeleteValueAsync(valueId, ct) is { } deactivated) return Ok(deactivated);
        return NoContent();
    }
}
