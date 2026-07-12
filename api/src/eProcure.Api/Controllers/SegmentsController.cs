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

    [HttpPost("{id:guid}/applications")]
    [Action(ApiActions.ManageSegments)]
    public async Task<ActionResult<SegmentDefDto>> Apply(Guid id, [FromBody] ApplySegmentRequest req, CancellationToken ct) =>
        Ok(await segments.ApplyAsync(id, req, ct));

    [HttpDelete("{id:guid}/applications/{recordType}")]
    [Action(ApiActions.ManageSegments)]
    public async Task<ActionResult<SegmentDefDto>> Unapply(Guid id, string recordType, CancellationToken ct) =>
        Ok(await segments.UnapplyAsync(id, recordType, ct));
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
        Ok(await segments.SaveAssignmentsAsync(recordType, recordId, req, ct));
}
