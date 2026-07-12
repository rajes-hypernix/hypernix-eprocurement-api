using eProcure.Api.Auth;
using eProcure.Application.Authorization;
using eProcure.Application.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

/// <summary>
/// Vendor-onboarding invitations + magic-link resolve (Slice B). Buyer endpoints create/resend/revoke
/// invitations and list onboarding question packs; the vendor endpoint resolves a magic-link token to
/// its scoped application (no login — the token is the scope, F2).
/// </summary>
[ApiController]
[Route("api/onboarding")]
public sealed class OnboardingController(IOnboardingService onboarding) : ControllerBase
{
    [HttpGet("templates")]
    [Action(ApiActions.ViewOnboarding)]
    public async Task<ActionResult<IReadOnlyList<OnboardingTemplateDto>>> Templates(CancellationToken ct) =>
        Ok(await onboarding.ListOnboardingTemplatesAsync(ct));

    [HttpGet("invitations")]
    [Action(ApiActions.ViewOnboarding)]
    public async Task<ActionResult<IReadOnlyList<OnboardingInvitationDto>>> Invitations(CancellationToken ct) =>
        Ok(await onboarding.ListInvitationsAsync(ct));

    [HttpPost("invitations")]
    [Action(ApiActions.InviteOnboarding)]
    public async Task<ActionResult<OnboardingInvitationDto>> Create(SendOnboardingInvitationRequest req, CancellationToken ct) =>
        Ok(await onboarding.CreateInvitationAsync(req, ct));

    [HttpPost("invitations/{id:guid}/resend")]
    [Action(ApiActions.InviteOnboarding)]
    public async Task<ActionResult<OnboardingInvitationDto>> Resend(Guid id, CancellationToken ct) =>
        Ok(await onboarding.ResendInvitationAsync(id, ct));

    [HttpPost("invitations/{id:guid}/revoke")]
    [Action(ApiActions.RevokeOnboardingInvitation)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await onboarding.RevokeInvitationAsync(id, ct);
        return NoContent();
    }

    /// <summary>Resolve a magic-link token to its application (opens it). Anonymous — the token is the scope.</summary>
    [AllowAnonymous]
    [HttpPost("resolve")]
    public async Task<ActionResult<OnboardingApplicationDto>> Resolve(ResolveOnboardingLinkRequest req, CancellationToken ct) =>
        Ok(await onboarding.ResolveTokenAsync(req.Token, ct));

    /// <summary>A2F-T2 (Obs-6): the reference lookups the anonymous form renders. POST with
    /// the token in the BODY (the /resolve precedent — tokens stay out of URLs and logs).</summary>
    [AllowAnonymous]
    [HttpPost("lookups")]
    public async Task<ActionResult<OnboardingLookupsDto>> Lookups(ResolveOnboardingLinkRequest req, CancellationToken ct) =>
        Ok(await onboarding.GetLookupsAsync(req.Token, ct));

    // ---- Slice C: the vendor onboarding form (token-scoped, no login) ----

    [AllowAnonymous]
    [HttpGet("draft")]
    public async Task<ActionResult<OnboardingDraftDto>> GetDraft([FromQuery] string token, CancellationToken ct) =>
        Ok(await onboarding.GetDraftAsync(token, ct));

    [AllowAnonymous]
    [HttpPut("draft")]
    public async Task<ActionResult<OnboardingDraftDto>> SaveDraft(SaveOnboardingDraftRequest req, CancellationToken ct) =>
        Ok(await onboarding.SaveDraftAsync(req, ct));

    [AllowAnonymous]
    [HttpPost("draft/submit")]
    public async Task<ActionResult<OnboardingDraftDto>> Submit(ResolveOnboardingLinkRequest req, CancellationToken ct) =>
        Ok(await onboarding.SubmitDraftAsync(req.Token, ct));

    [AllowAnonymous]
    [HttpPost("draft/documents")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<OnboardingDocumentDto>> UploadDocument(
        [FromQuery] string token, [FromQuery] string key, IFormFile file, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        return Ok(await onboarding.UploadDocumentAsync(token, key, file.FileName, file.ContentType, ms.ToArray(), ct));
    }

    [AllowAnonymous]
    [HttpDelete("draft/documents/{key}")]
    public async Task<IActionResult> DeleteDocument([FromQuery] string token, string key, CancellationToken ct)
    {
        await onboarding.DeleteDocumentAsync(token, key, ct);
        return NoContent();
    }

    // ---- Slice D: buyer review + clarification + approve/reject/promote ----

    [HttpGet("applications")]
    [Action(ApiActions.ViewOnboarding)]
    public async Task<ActionResult<IReadOnlyList<OnboardingQueueItemDto>>> Applications(CancellationToken ct) =>
        Ok(await onboarding.ListApplicationsAsync(ct));

    [HttpGet("applications/{id:guid}")]
    [Action(ApiActions.ViewOnboarding)]
    public async Task<ActionResult<OnboardingReviewDto>> Application(Guid id, CancellationToken ct) =>
        Ok(await onboarding.GetApplicationAsync(id, ct));

    [HttpPost("applications/{id:guid}/start-review")]
    [Action(ApiActions.ReviewOnboardingApplication)]
    public async Task<ActionResult<OnboardingReviewDto>> StartReview(Guid id, CancellationToken ct) =>
        Ok(await onboarding.StartReviewAsync(id, ct));

    [HttpPost("applications/{id:guid}/clarify")]
    [Action(ApiActions.ReviewOnboardingApplication)]
    public async Task<ActionResult<OnboardingReviewDto>> Clarify(Guid id, RequestClarificationRequest req, CancellationToken ct) =>
        Ok(await onboarding.RequestClarificationAsync(id, req, ct));

    [HttpPost("applications/{id:guid}/approve")]
    [Action(ApiActions.ReviewOnboardingApplication)]
    public async Task<ActionResult<OnboardingApproveResultDto>> Approve(Guid id, CancellationToken ct) =>
        Ok(await onboarding.ApproveAsync(id, ct));

    [HttpPost("applications/{id:guid}/reject")]
    [Action(ApiActions.ReviewOnboardingApplication)]
    public async Task<ActionResult<OnboardingReviewDto>> Reject(Guid id, RejectOnboardingRequest req, CancellationToken ct) =>
        Ok(await onboarding.RejectAsync(id, req.Reason, ct));

    // Vendor-side (token-scoped, no login).
    [AllowAnonymous]
    [HttpPost("draft/resubmit")]
    public async Task<ActionResult<OnboardingApplicationDto>> Resubmit(ResubmitOnboardingRequest req, CancellationToken ct) =>
        Ok(await onboarding.ResubmitAsync(req, ct));

    [AllowAnonymous]
    [HttpPost("draft/raise-clarification")]
    public async Task<ActionResult<OnboardingApplicationDto>> RaiseClarification(RaiseClarificationRequest req, CancellationToken ct) =>
        Ok(await onboarding.RaiseClarificationAsync(req, ct));
}
