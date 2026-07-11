using eProcure.Api.Auth;
using eProcure.Application.Authorization;
using eProcure.Application.Sourcing;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class AwardsController(IAwardService awards) : ControllerBase
{
    /// <summary>Award allocation view — eligible vendors + ranking. Commercial pricing is
    /// omitted until the reveal gate (BUSINESS-RULES [G]).</summary>
    [HttpGet("rfqs/{rfqId:guid}/award-eligibility")]
    [Action(ApiActions.ViewAwards)]
    public async Task<ActionResult<AwardEligibilityDto>> Eligibility(Guid rfqId, CancellationToken ct)
    {
        var dto = await awards.GetEligibilityAsync(rfqId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("rfqs/{rfqId:guid}/award")]
    [Action(ApiActions.ViewAwards)]
    public async Task<ActionResult<AwardDto>> ForRfq(Guid rfqId, CancellationToken ct)
    {
        var dto = await awards.GetForRfqAsync(rfqId, ct);
        return dto is null ? NoContent() : Ok(dto);
    }

    [HttpPost("rfqs/{rfqId:guid}/award")]
    [Action(ApiActions.SubmitAward)]
    public async Task<ActionResult<AwardDto>> Submit(Guid rfqId, [FromBody] SubmitAwardRequest req, CancellationToken ct) =>
        Ok(await awards.SubmitForApprovalAsync(rfqId, req, ct));

    [HttpPost("awards/{awardId:guid}/approve")]
    [Action(ApiActions.ApproveAward)]
    public async Task<ActionResult<AwardDto>> Approve(Guid awardId, CancellationToken ct) =>
        Ok(await awards.ApproveAsync(awardId, ct));

    [HttpGet("awards")]
    [Action(ApiActions.ViewAwards)]
    public async Task<ActionResult<IReadOnlyList<AwardDto>>> List(CancellationToken ct) =>
        Ok(await awards.ListAsync(ct));
}
