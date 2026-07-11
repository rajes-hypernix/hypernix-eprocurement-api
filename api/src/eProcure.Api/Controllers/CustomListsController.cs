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

    [HttpPost("{code}/values")]
    [Action(ApiActions.ManageCustomLists)]
    public async Task<ActionResult<CustomListValueDto>> AddValue(string code, AddCustomListValueRequest req, CancellationToken ct) =>
        Ok(await lists.AddValueAsync(code, req, ct));

    [HttpPut("values/{valueId:guid}")]
    [Action(ApiActions.ManageCustomLists)]
    public async Task<ActionResult<CustomListValueDto>> UpdateValue(Guid valueId, UpdateCustomListValueRequest req, CancellationToken ct) =>
        Ok(await lists.UpdateValueAsync(valueId, req, ct));

    [HttpDelete("values/{valueId:guid}")]
    [Action(ApiActions.ManageCustomLists)]
    public async Task<IActionResult> DeleteValue(Guid valueId, CancellationToken ct)
    {
        await lists.DeleteValueAsync(valueId, ct);
        return NoContent();
    }
}
