using eProcure.Application.Abstractions;
using eProcure.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(AppDbContext db, IClock clock) : ControllerBase
{
    public sealed record HealthResponse(string Status, string Database, DateTime TimeUtc);

    /// <summary>Liveness + DB connectivity probe. Anonymous by design (no principal, no data).</summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken ct)
    {
        var dbOk = await db.Database.CanConnectAsync(ct);
        return Ok(new HealthResponse(
            Status: "ok",
            Database: dbOk ? "connected" : "unavailable",
            TimeUtc: clock.UtcNow));
    }
}
