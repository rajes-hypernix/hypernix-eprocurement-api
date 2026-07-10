using eProcure.Application.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(IDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken ct) => Ok(await dashboard.GetAsync(ct));
}
