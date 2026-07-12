using eProcure.Api.Auth;
using eProcure.Application.Authorization;
using eProcure.Application.CustomFields;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

/// <summary>Custom field DEFINITIONS — the Admin Setup surface (AUTHORIZATION-MATRIX A65).</summary>
[ApiController]
[Route("api/custom-fields")]
public sealed class CustomFieldsController(ICustomFieldService fields) : ControllerBase
{
    [HttpGet]
    [Action(ApiActions.ManageCustomFields)]
    public async Task<ActionResult<IReadOnlyList<CustomFieldDefDto>>> List([FromQuery] string? recordType, CancellationToken ct) =>
        Ok(await fields.ListDefsAsync(recordType, ct));

    [HttpPost]
    [Action(ApiActions.ManageCustomFields)]
    public async Task<ActionResult<CustomFieldDefDto>> Create([FromBody] SaveCustomFieldDefRequest req, CancellationToken ct) =>
        Ok(await fields.CreateDefAsync(req, ct));

    [HttpPut("{id:guid}")]
    [Action(ApiActions.ManageCustomFields)]
    public async Task<ActionResult<CustomFieldDefDto>> Update(Guid id, [FromBody] SaveCustomFieldDefRequest req, CancellationToken ct) =>
        Ok(await fields.UpdateDefAsync(id, req, ct));

    /// <summary>Deactivate/reactivate — values persist; deactivated fields hide from entry
    /// surfaces and the view-builder palette; a view referencing one fails loudly (ruled).</summary>
    [HttpPost("{id:guid}/active")]
    [Action(ApiActions.ManageCustomFields)]
    public async Task<ActionResult<CustomFieldDefDto>> SetActive(Guid id, [FromBody] bool active, CancellationToken ct) =>
        Ok(await fields.SetDefActiveAsync(id, active, ct));

    /// <summary>Zero-value defs only (typo cleanup, ruled) — any values ever written → 409.</summary>
    [HttpDelete("{id:guid}")]
    [Action(ApiActions.ManageCustomFields)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await fields.DeleteDefAsync(id, ct);
        return NoContent();
    }
}

/// <summary>
/// Custom field VALUES per record (A66/A67): the static action is layer 1; the service adds
/// the record type's dynamic View* check and rides its EXISTING scoped detail fetch (layers
/// 2+3 — the third use of the established convention).
/// </summary>
[ApiController]
[Route("api/custom-values")]
public sealed class CustomValuesController(ICustomFieldService fields) : ControllerBase
{
    [HttpGet("{recordType}/{recordId:guid}")]
    [Action(ApiActions.ReadCustomValues)]
    public async Task<ActionResult<IReadOnlyList<CustomValueDto>>> Get(string recordType, Guid recordId, CancellationToken ct) =>
        Ok(await fields.GetValuesAsync(recordType, recordId, ct));

    // CF6-T1: line-grain reads — { lineId: CustomValueDto[] } for the record's Line-scope defs.
    [HttpGet("{recordType}/{recordId:guid}/lines")]
    [Action(ApiActions.ReadCustomValues)]
    public async Task<ActionResult<IReadOnlyDictionary<Guid, IReadOnlyList<CustomValueDto>>>> GetLines(string recordType, Guid recordId, CancellationToken ct) =>
        Ok(await fields.GetLineValuesAsync(recordType, recordId, ct));

    [HttpPut("{recordType}/{recordId:guid}")]
    [Action(ApiActions.EditCustomValues)]
    public async Task<ActionResult<IReadOnlyList<CustomValueDto>>> Save(string recordType, Guid recordId, [FromBody] SaveCustomValuesRequest req, CancellationToken ct) =>
        Ok(await fields.SaveValuesAsync(recordType, recordId, req, ct));
}
