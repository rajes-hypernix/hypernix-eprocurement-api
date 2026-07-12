using eProcure.Api.Auth;
using eProcure.Application.Authorization;
using eProcure.Application.Dashboards;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

/// <summary>
/// Dashboards + the metric layer (D4). Actions per AUTHORIZATION-MATRIX A62–A64:
/// using dashboards/metrics is all-principal (metric reads ADDITIONALLY check the
/// metric's RequiredAction inside the service — the ruled per-metric dynamic gate);
/// role defaults are platform configuration, Admin only.
/// </summary>
[ApiController]
[Route("api/dashboards")]
public sealed class DashboardsController(IDashboardStore store) : ControllerBase
{
    [HttpGet("mine")]
    [Action(ApiActions.UseDashboards)]
    public async Task<ActionResult<UserDashboardDto>> Mine(CancellationToken ct) =>
        Ok(await store.MineAsync(ct));

    /// <summary>Copy-on-write: snapshots the resolved role-default union into a user-owned copy.</summary>
    [HttpPost("personalize")]
    [Action(ApiActions.ManageOwnDashboard)]
    public async Task<ActionResult<UserDashboardDto>> Personalize(CancellationToken ct) =>
        Ok(await store.PersonalizeAsync(ct));

    [HttpPut("mine")]
    [Action(ApiActions.ManageOwnDashboard)]
    public async Task<ActionResult<UserDashboardDto>> UpdateMine([FromBody] UpdateDashboardRequest req, CancellationToken ct) =>
        Ok(await store.UpdateMineAsync(req, ct));

    /// <summary>Reset-to-default: deletes the personal copy; resolution falls back to the role default.</summary>
    [HttpDelete("mine")]
    [Action(ApiActions.ManageOwnDashboard)]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        await store.ResetAsync(ct);
        return NoContent();
    }

    [HttpPut("role-defaults/{role}")]
    [Action(ApiActions.ManageRoleDashboards)]
    public async Task<ActionResult<UserDashboardDto>> UpdateRoleDefault(string role, [FromBody] UpdateDashboardRequest req, CancellationToken ct) =>
        Ok(await store.UpdateRoleDefaultAsync(role, req, ct));
}

[ApiController]
[Route("api/metrics")]
public sealed class MetricsController(ISystemMetricService metrics) : ControllerBase
{
    [HttpGet]
    [Action(ApiActions.UseDashboards)]
    public ActionResult<IReadOnlyList<MetricDescriptor>> Catalog() => Ok(metrics.Catalog);

    [HttpGet("{id}/value")]
    [Action(ApiActions.UseDashboards)]
    public async Task<ActionResult<MetricValueDto>> Value(string id, CancellationToken ct) =>
        Ok(await metrics.ValueAsync(id, ct));

    [HttpGet("{id}/series")]
    [Action(ApiActions.UseDashboards)]
    public async Task<ActionResult<MetricSeriesDto>> Series(string id, [FromQuery] int months = 12, CancellationToken ct = default) =>
        Ok(await metrics.SeriesAsync(id, months, ct));
}
