using eProcure.Api.Auth;
using eProcure.Application.Authorization;
using eProcure.Application.Views;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

/// <summary>
/// Saved views engine (D3) — additive; no list endpoint is replaced. Actions per
/// AUTHORIZATION-MATRIX A59–A61: using views is all-principal (the run additionally
/// checks the record type's View* action inside the service — the ruled dynamic gate);
/// managing shared views is publication, Buyer/Admin only, carried by its own endpoint
/// so the no-orphan drift sweep holds.
/// </summary>
[ApiController]
[Route("api/views")]
public sealed class ViewsController(ISavedViewService views) : ControllerBase
{
    [HttpGet]
    [Action(ApiActions.UseSavedViews)]
    public async Task<ActionResult<IReadOnlyList<SavedViewDto>>> List([FromQuery] string? recordType, CancellationToken ct) =>
        Ok(await views.ListVisibleAsync(recordType, ct));

    /// <summary>The ViewBuilder's field palette: registry rows + enum options for the record type.</summary>
    [HttpGet("fields")]
    [Action(ApiActions.UseSavedViews)]
    public async Task<ActionResult<IReadOnlyList<ViewFieldDto>>> Fields([FromQuery] string recordType, CancellationToken ct) =>
        Ok(await views.FieldsAsync(recordType, ct));

    [HttpPost]
    [Action(ApiActions.ManageOwnSavedViews)]
    public async Task<ActionResult<SavedViewDto>> Create([FromBody] SaveViewRequest req, CancellationToken ct) =>
        Ok(await views.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [Action(ApiActions.ManageOwnSavedViews)]
    public async Task<ActionResult<SavedViewDto>> Update(Guid id, [FromBody] SaveViewRequest req, CancellationToken ct) =>
        Ok(await views.UpdateAsync(id, req, ct));

    [HttpDelete("{id:guid}")]
    [Action(ApiActions.ManageOwnSavedViews)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await views.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/share")]
    [Action(ApiActions.ManageSharedViews)]
    public async Task<ActionResult<SavedViewDto>> Share(Guid id, [FromBody] ShareViewRequest req, CancellationToken ct) =>
        Ok(await views.ShareAsync(id, req.IsShared, ct));

    /// <summary>The heart: typed rows shaped by the view's columns, built on the scoped
    /// sources (D3 Step 0(c)). D7.5: paged (default 50, cap 200) — like D6's groupBy,
    /// query params on an existing A59 read add no endpoint and no action.</summary>
    [HttpGet("{id:guid}/run")]
    [Action(ApiActions.UseSavedViews)]
    public async Task<ActionResult<ViewRunResult>> Run(Guid id, [FromQuery] int page = 1, [FromQuery] int size = 50, CancellationToken ct = default) =>
        Ok(await views.RunAsync(id, page, size, ct));

    /// <summary>D4 aggregation seam: count|sum|avg over the SAME pipeline as the run —
    /// same visibility, same record-type View* check, same scoped sources.</summary>
    [HttpGet("{id:guid}/aggregate")]
    [Action(ApiActions.UseSavedViews)]
    public async Task<ActionResult<Application.Dashboards.ViewAggregateResult>> Aggregate(
        Guid id, [FromQuery] string fn, [FromQuery] string? field, [FromQuery] string? groupBy, CancellationToken ct) =>
        Ok(await views.AggregateAsync(id, fn, field, groupBy, ct));

    /// <summary>Month-bucketed series for chart portlets; null bucket-field rows are excluded
    /// and surfaced as unbucketedCount (the honest-data rule applied to time).</summary>
    [HttpGet("{id:guid}/series")]
    [Action(ApiActions.UseSavedViews)]
    public async Task<ActionResult<Application.Dashboards.ViewSeriesResult>> Series(
        Guid id, [FromQuery] string fn, [FromQuery] string? field, [FromQuery] string bucket, [FromQuery] int months = 12, [FromQuery] string? groupBy = null, CancellationToken ct = default) =>
        Ok(await views.SeriesAsync(id, fn, field, bucket, months, groupBy, ct));
}
