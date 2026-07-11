using eProcure.Api.Auth;
using eProcure.Application.Authorization;
using eProcure.Application.Communication;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

[ApiController]
[Route("api/clarifications")]
public sealed class ClarificationsController(IClarificationService clarifications) : ControllerBase
{
    [HttpGet]
    [Action(ApiActions.ViewClarifications)]
    public async Task<ActionResult<IReadOnlyList<ClarificationThreadDto>>> List(CancellationToken ct) =>
        Ok(await clarifications.ListThreadsAsync(ct));

    [HttpGet("thread")]
    [Action(ApiActions.ViewClarifications)]
    public async Task<ActionResult<ClarificationThreadDetail>> Thread([FromQuery] string scope, [FromQuery] Guid vendorId, CancellationToken ct)
    {
        var t = await clarifications.GetThreadAsync(scope, vendorId, ct);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpPost]
    [Action(ApiActions.SendClarification)]
    public async Task<ActionResult<ClarificationThreadDetail>> Send([FromBody] SendClarificationRequest req, CancellationToken ct) =>
        Ok(await clarifications.SendAsync(req, ct));
}
