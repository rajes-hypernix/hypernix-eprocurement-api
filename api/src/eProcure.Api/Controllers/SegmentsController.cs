using eProcure.Api.Auth;
using eProcure.Application.Authorization;
using eProcure.Application.Segments;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

/// <summary>Segment DEFINITIONS/values/applications — Admin Setup (AUTHORIZATION-MATRIX A68).</summary>
[ApiController]
[Route("api/segments")]
public sealed class SegmentsController(ISegmentService segments) : ControllerBase
{
    [HttpGet]
    [Action(ApiActions.ManageSegments)]
    public async Task<ActionResult<IReadOnlyList<SegmentDefDto>>> List(CancellationToken ct) =>
        Ok(await segments.ListDefsAsync(ct));

    [HttpPost]
    [Action(ApiActions.ManageSegments)]
    public async Task<ActionResult<SegmentDefDto>> Create([FromBody] SaveSegmentDefRequest req, CancellationToken ct) =>
        Ok(await segments.CreateDefAsync(req, ct));

    [HttpPut("{id:guid}")]
    [Action(ApiActions.ManageSegments)]
    public async Task<ActionResult<SegmentDefDto>> Update(Guid id, [FromBody] SaveSegmentDefRequest req, CancellationToken ct) =>
        Ok(await segments.UpdateDefAsync(id, req, ct));

    [HttpPost("{id:guid}/values")]
    [Action(ApiActions.ManageSegments)]
    public async Task<ActionResult<SegmentDefDto>> AddValue(Guid id, [FromBody] SaveSegmentValueRequest req, CancellationToken ct) =>
        Ok(await segments.AddValueAsync(id, req, ct));

    [HttpPut("{id:guid}/values/{valueId:guid}")]
    [Action(ApiActions.ManageSegments)]
    public async Task<ActionResult<SegmentDefDto>> UpdateValue(Guid id, Guid valueId, [FromBody] UpdateSegmentValueRequest req, CancellationToken ct) =>
        Ok(await segments.UpdateValueAsync(id, valueId, req, ct));

    [HttpDelete("{id:guid}/values/{valueId:guid}")]
    [Action(ApiActions.ManageSegments)]
    public async Task<ActionResult<SegmentDefDto>> DeleteValue(Guid id, Guid valueId, CancellationToken ct) =>
        Ok(await segments.DeleteValueAsync(id, valueId, ct));

    [HttpPost("{id:guid}/active")]
    [Action(ApiActions.ManageSegments)]
    public async Task<ActionResult<SegmentDefDto>> SetActive(Guid id, [FromBody] bool active, CancellationToken ct) =>
        Ok(await segments.SetDefActiveAsync(id, active, ct));

    [HttpDelete("{id:guid}")]
    [Action(ApiActions.ManageSegments)]
    public async Task<IActionResult> DeleteDef(Guid id, CancellationToken ct)
    {
        await segments.DeleteDefAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/applications")]
    [Action(ApiActions.ManageSegments)]
    public async Task<ActionResult<SegmentDefDto>> Apply(Guid id, [FromBody] ApplySegmentRequest req, CancellationToken ct) =>
        Ok(await segments.ApplyAsync(id, req, ct));

    [HttpDelete("{id:guid}/applications/{recordType}")]
    [Action(ApiActions.ManageSegments)]
    public async Task<ActionResult<SegmentDefDto>> Unapply(Guid id, string recordType, CancellationToken ct) =>
        Ok(await segments.UnapplyAsync(id, recordType, ct));

    // CF-FIX4-T6: the CF-FIX-3 lifecycle surface, segment grain. Reports are admin-scoped;
    // purge is the DISTINCT higher tier (A73 — deliberately not implied by ManageSegments).
    [HttpGet("{id:guid}/references")]
    [Action(ApiActions.ManageSegments)]
    public async Task<ActionResult<eProcure.Application.CustomFields.ImpactReportDto>> References(Guid id, CancellationToken ct) =>
        Ok(await segments.GetReferencesAsync(id, ct));

    [HttpPost("{id:guid}/purge")]
    [Action(ApiActions.PurgeCustomFieldHistory)]
    public async Task<IActionResult> Purge(Guid id, CancellationToken ct)
    {
        await segments.PurgeAsync(id, ct);
        return NoContent();
    }

    [HttpGet("values/{valueId:guid}/references")]
    [Action(ApiActions.ManageSegments)]
    public async Task<ActionResult<eProcure.Application.CustomFields.ImpactReportDto>> ValueReferences(Guid valueId, CancellationToken ct) =>
        Ok(await segments.GetValueReferencesAsync(valueId, ct));

    [HttpPost("values/{valueId:guid}/purge")]
    [Action(ApiActions.PurgeCustomFieldHistory)]
    public async Task<IActionResult> PurgeValue(Guid valueId, CancellationToken ct)
    {
        await segments.PurgeValueAsync(valueId, ct);
        return NoContent();
    }
}

/// <summary>
/// Segment ASSIGNMENTS per record/line — the folded A66/A67 gate (ruled: same species as
/// custom values, same three layers; if the role sets ever diverge, that is when the rows split).
/// </summary>
[ApiController]
[Route("api/segment-assignments")]
public sealed class SegmentAssignmentsController(ISegmentService segments) : ControllerBase
{
    [HttpGet("{recordType}/{recordId:guid}")]
    [Action(ApiActions.ReadCustomValues)]
    public async Task<ActionResult<IReadOnlyList<SegmentAssignmentDto>>> Get(string recordType, Guid recordId, [FromQuery] Guid? lineId, CancellationToken ct) =>
        Ok(await segments.GetAssignmentsAsync(recordType, recordId, lineId, ct));

    [HttpPut("{recordType}/{recordId:guid}")]
    [Action(ApiActions.EditCustomValues)]
    public async Task<ActionResult<IReadOnlyList<SegmentAssignmentDto>>> Save(string recordType, Guid recordId, [FromBody] SaveSegmentAssignmentsRequest req, CancellationToken ct) =>
        Ok(await segments.SaveAssignmentsAsync(recordType, recordId, req, ct));}
