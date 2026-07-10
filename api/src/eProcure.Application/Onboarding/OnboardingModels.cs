using eProcure.Domain.Onboarding;

namespace eProcure.Application.Onboarding;

// ---- Options (config-bound; no secrets in code — VENDOR-ONBOARDING-SPEC §6) ----

/// <summary>Onboarding configuration (bound from the "Onboarding" section).</summary>
public sealed class OnboardingOptions
{
    /// <summary>Web origin the magic link points at (the vendor landing).</summary>
    public string PortalBaseUrl { get; set; } = "http://localhost:5173/";

    /// <summary>Non-production override: when set, ALL onboarding email is routed here (E3).</summary>
    public string? TestRecipientOverride { get; set; }

    /// <summary>Magic-link validity in days (SPEC §1.2).</summary>
    public int LinkExpiryDays { get; set; } = 14;

    /// <summary>The default vendor email the invite form pre-fills (E2).</summary>
    public string DefaultVendorEmail { get; set; } = "vieshall@hypernix.net";
}

// ---- DTOs ----

/// <summary>An onboarding question pack offered at invite (an Onboarding-purpose FormTemplate).</summary>
public sealed record OnboardingTemplateDto(Guid Id, string Code, string Name, int QuestionCount);

/// <summary>An invitation as returned to the buyer. <see cref="MagicLink"/> is included for the
/// demo confirmation drawer (the same link that was emailed).</summary>
public sealed record OnboardingInvitationDto(
    Guid Id, string Email, string Type, string Status, string InvitedByName,
    DateTime CreatedUtc, DateTime ExpiresUtc, Guid? ApplicationId, string? ApplicationCode,
    string MagicLink);

/// <summary>The scoped application a resolved magic link returns to the vendor landing. Includes any
/// clarification rounds so the vendor can see (and answer) an open one.</summary>
public sealed record OnboardingApplicationDto(
    Guid Id, string Code, string Status, string Type, string Name, string Email,
    IReadOnlyList<OnboardingTemplateDto> Packs, IReadOnlyList<OnboardingRoundDto> Rounds,
    DateTime CreatedUtc, DateTime? SubmittedUtc);

/// <summary>Buyer request to send an invitation (SPEC §1.2). Email defaults server-side to
/// <see cref="OnboardingOptions.DefaultVendorEmail"/> if blank; type is "SWEC" or "Non-SWEC".</summary>
public sealed record SendOnboardingInvitationRequest(
    string? Email, string Type, string? Name, IReadOnlyList<Guid> SelectedTemplateIds);

/// <summary>Vendor request to resolve a magic-link token.</summary>
public sealed record ResolveOnboardingLinkRequest(string Token);

// ---- Slice C: the editable draft (vendor onboarding form) ----

/// <summary>One question pack WITH its form items, so the vendor form renders it with the existing
/// renderer (D3). Items mirror <c>FormItem</c>.</summary>
public sealed record OnboardingPackDto(Guid Id, string Code, string Name, IReadOnlyList<OnboardingFormItemDto> Items);
public sealed record OnboardingFormItemDto(
    string Kind, string Group, string Section, string Label, string Type, bool Required,
    string Config, string Help, int Order);

public sealed record OnboardingBankDto(string Bank, string AccountNo, string Swift);
public sealed record OnboardingAnswerDto(Guid FormTemplateId, int QuestionOrder, string Value);
public sealed record OnboardingDocumentDto(string Key, string FileName, Guid StoredFileId);

/// <summary>One year of the 11 financial line items (RM'000). Empty for SWEC (waiver).</summary>
public sealed record OnboardingFinancialYearDto(
    int YearIndex, decimal Revenue, decimal NetProfit, decimal Ebit, decimal TotalAssets,
    decimal CurrentAssets, decimal Inventory, decimal CurrentLiabilities, decimal TotalLiabilities,
    decimal Equity, decimal RetainedEarnings, decimal FixedAssets);

/// <summary>The full editable onboarding draft returned to the vendor form (save-and-resume).</summary>
public sealed record OnboardingDraftDto(
    Guid Id, string Code, string Status, string Type,
    string Name, string RegistrationNo, string Location, string Email, string ContactName, string ContactPhone,
    OnboardingBankDto Bank, IReadOnlyList<string> Categories,
    IReadOnlyList<OnboardingFinancialYearDto> Financials, IReadOnlyList<OnboardingAnswerDto> Answers,
    IReadOnlyList<OnboardingDocumentDto> Documents, IReadOnlyList<OnboardingPackDto> Packs,
    string? FinancialBand, DateTime? SubmittedUtc,
    string Country, string State);   // conformed geography (Custom List codes)

/// <summary>Autosave payload — a partial patch of the draft (null members are left unchanged).</summary>
public sealed record SaveOnboardingDraftRequest(
    string Token,
    string? Name, string? RegistrationNo, string? Location, string? Email, string? ContactName, string? ContactPhone,
    OnboardingBankDto? Bank, IReadOnlyList<string>? Categories,
    IReadOnlyList<OnboardingFinancialYearDto>? Financials, IReadOnlyList<OnboardingAnswerDto>? Answers,
    string? Country = null, string? State = null);

// ---- Slice D: buyer review + clarification + approve/reject/promote ----

/// <summary>One row in the buyer's onboarding queue (status + clarification-round count).</summary>
public sealed record OnboardingQueueItemDto(
    Guid Id, string Code, string Name, string Type, string Status, string Source,
    DateTime CreatedUtc, DateTime? SubmittedUtc, int? OpenRoundNo, int RoundCount, Guid? InvitationId);

public sealed record OnboardingRoundItemDto(string Topic, string Request, string Response);
public sealed record OnboardingRoundDto(
    int RoundNo, string Direction, string Status, string Message, string RaisedByName,
    DateTime RaisedUtc, DateTime? RespondedUtc, IReadOnlyList<OnboardingRoundItemDto> Items);

/// <summary>An answer shown in review with its question label + pack.</summary>
public sealed record OnboardingAnswerViewDto(string Pack, string Label, string Value);

/// <summary>Per-year Z′ components (X1..X5) + the year's Z, for the "view calculation" drawer.</summary>
public sealed record OnboardingFinancialYearCalcDto(
    int YearIndex, double X1, double X2, double X3, double X4, double X5, double Z);

/// <summary>The financial band + snapshot-style figures + the calc breakdown (Non-SWEC only).</summary>
public sealed record OnboardingFinancialViewDto(
    string Band, string Risk, string Zone, double WeightedZ, int Score, string Statement,
    IReadOnlyList<OnboardingFinancialYearCalcDto> Years);

/// <summary>The full buyer review of one application.</summary>
public sealed record OnboardingReviewDto(
    Guid Id, string Code, string Status, string Type, string Source,
    string Name, string RegistrationNo, string Location, string Email, string ContactName, string ContactPhone,
    OnboardingBankDto Bank, IReadOnlyList<string> Categories,
    OnboardingFinancialViewDto? Financial, IReadOnlyList<OnboardingAnswerViewDto> Answers,
    IReadOnlyList<OnboardingDocumentDto> Documents, IReadOnlyList<OnboardingRoundDto> Rounds,
    string? DuplicateWarning, DateTime? SubmittedUtc, DateTime? DecisionUtc, Guid? PromotedVendorId, string? RejectReason);

public sealed record ClarificationItemInput(string Topic, string Request);
public sealed record RequestClarificationRequest(string Message, IReadOnlyList<ClarificationItemInput> Items);
public sealed record RaiseClarificationRequest(string Token, string Message, IReadOnlyList<ClarificationItemInput> Items);
public sealed record ResubmitOnboardingRequest(string Token, IReadOnlyList<string> Responses);
public sealed record RejectOnboardingRequest(string Reason);

/// <summary>The outcome of an approval: the master vendor created, plus any duplicate warning surfaced.</summary>
public sealed record OnboardingApproveResultDto(Guid VendorId, string VendorCode, string? DuplicateWarning);

// ---- Services ----

/// <summary>Onboarding invitation + magic-link use-cases (Slice B). Buyer-facing create/resend/revoke
/// and the vendor-facing token resolve. Every transition audits and stamps time via IClock.</summary>
public interface IOnboardingService
{
    Task<IReadOnlyList<OnboardingTemplateDto>> ListOnboardingTemplatesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OnboardingInvitationDto>> ListInvitationsAsync(CancellationToken ct = default);
    Task<OnboardingInvitationDto> CreateInvitationAsync(SendOnboardingInvitationRequest req, CancellationToken ct = default);
    Task<OnboardingInvitationDto> ResendInvitationAsync(Guid invitationId, CancellationToken ct = default);
    Task RevokeInvitationAsync(Guid invitationId, CancellationToken ct = default);

    /// <summary>Resolves a magic-link token to its scoped application (Invited → InProgress on first
    /// open — A2). Throws on an invalid / expired / revoked token.</summary>
    Task<OnboardingApplicationDto> ResolveTokenAsync(string rawToken, CancellationToken ct = default);

    // ---- Slice C: the onboarding form (all token-scoped — the token is the access scope, F2) ----

    /// <summary>Reads the full editable draft for a token (save-and-resume).</summary>
    Task<OnboardingDraftDto> GetDraftAsync(string rawToken, CancellationToken ct = default);

    /// <summary>Autosaves a partial draft patch. Only valid while the application is in progress.</summary>
    Task<OnboardingDraftDto> SaveDraftAsync(SaveOnboardingDraftRequest req, CancellationToken ct = default);

    /// <summary>Submits the application (A3): validates required core fields, snapshots the financial
    /// assessment (Non-SWEC), and moves InProgress → Submitted.</summary>
    Task<OnboardingDraftDto> SubmitDraftAsync(string rawToken, CancellationToken ct = default);

    /// <summary>Attaches/replaces a checklist document (StoredFile) on the application.</summary>
    Task<OnboardingDocumentDto> UploadDocumentAsync(string rawToken, string key, string fileName,
        string contentType, byte[] content, CancellationToken ct = default);

    /// <summary>Removes a checklist document from the application.</summary>
    Task DeleteDocumentAsync(string rawToken, string key, CancellationToken ct = default);

    // ---- Slice D: buyer review + clarification + approve/reject/promote ----

    /// <summary>The buyer's onboarding queue (all non-draft applications).</summary>
    Task<IReadOnlyList<OnboardingQueueItemDto>> ListApplicationsAsync(CancellationToken ct = default);

    /// <summary>The buyer review of one application (by id).</summary>
    Task<OnboardingReviewDto> GetApplicationAsync(Guid id, CancellationToken ct = default);

    /// <summary>Buyer picks the application up for review (A4). Submitted/Resubmitted → UnderReview.</summary>
    Task<OnboardingReviewDto> StartReviewAsync(Guid id, CancellationToken ct = default);

    /// <summary>Buyer raises one batched clarification round → one email (A7).</summary>
    Task<OnboardingReviewDto> RequestClarificationAsync(Guid id, RequestClarificationRequest req, CancellationToken ct = default);

    /// <summary>Vendor answers the open round and resubmits it (A8). Token-scoped.</summary>
    Task<OnboardingApplicationDto> ResubmitAsync(ResubmitOnboardingRequest req, CancellationToken ct = default);

    /// <summary>Vendor raises a clarification round back to the buyer (both directions, SPEC §4). Token-scoped.</summary>
    Task<OnboardingApplicationDto> RaiseClarificationAsync(RaiseClarificationRequest req, CancellationToken ct = default);

    /// <summary>Approve → promote to the Vendor master + provision a VendorUser + copy the assessment (A5, §8).</summary>
    Task<OnboardingApproveResultDto> ApproveAsync(Guid id, CancellationToken ct = default);

    /// <summary>Reject with a reason (A6). Master untouched.</summary>
    Task<OnboardingReviewDto> RejectAsync(Guid id, string reason, CancellationToken ct = default);
}

/// <summary>Composes and sends onboarding emails. The non-production recipient override (E3) is
/// applied here so it covers every onboarding message, not just the invitation.</summary>
public interface IOnboardingNotifier
{
    /// <summary>Sends the invitation email carrying the magic link.</summary>
    Task SendInvitationAsync(VendorOnboardingInvitation invitation, string applicationCode,
        string rawToken, CancellationToken ct = default);

    /// <summary>Notifies the vendor a clarification round was raised (E4), listing its items.</summary>
    Task SendClarificationAsync(string toEmail, string applicationCode, string message,
        IReadOnlyList<(string Topic, string Request)> items, CancellationToken ct = default);

    /// <summary>Notifies the vendor of approval with the set-password link (E4).</summary>
    Task SendApprovedAsync(string toEmail, string applicationCode, string vendorCode,
        string setPasswordLink, CancellationToken ct = default);

    /// <summary>Notifies the vendor of rejection with the reason (E4).</summary>
    Task SendRejectedAsync(string toEmail, string applicationCode, string reason, CancellationToken ct = default);

    /// <summary>Builds the magic link for a raw token (shared by the notifier and the buyer DTO).</summary>
    string BuildMagicLink(string rawToken);
}
