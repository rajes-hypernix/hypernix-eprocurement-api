using eProcure.Domain.Suppliers;

namespace eProcure.Domain.Onboarding;

/// <summary>
/// A vendor-onboarding application — the aggregate root (grain: one application;
/// DATA-MODEL-ANALYTICS §7). This is the <b>staging</b> record: everything the vendor types lives
/// here and NEVER touches the <see cref="Vendor"/> master until <see cref="Approve"/> promotes it
/// (GOLDEN CONSTRAINT 2). Mirrors the master's shape (profile scalars + owned contacts/addresses/
/// banking/certs) so promotion is a copy. Owns its financial assessment, clarification rounds,
/// questionnaire answers, and the workflow-seam review steps.
///
/// Every lifecycle move is a method here (rich domain), throws <see cref="DomainRuleException"/> on
/// an illegal transition (A11), stamps time via the supplied UTC, and returns an
/// <see cref="OnboardingTransition"/> the calling service records as a typed <c>AuditEntry</c>.
/// </summary>
public class VendorOnboardingApplication
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; } = default!;      // VOB-2026-0007 (business key, immutable)

    public OnboardingStatus Status { get; private set; } = OnboardingStatus.Invited;
    public ApplicationSource Source { get; private set; } = ApplicationSource.SelfService;
    public VendorType Type { get; private set; } = VendorType.NonSwec;   // set at creation; drives waiver + promoted status

    /// <summary>The invitation that opened this application (null for manual entry).</summary>
    public Guid? InvitationId { get; private set; }

    // --- Staging profile (mirrors Vendor master; free-text draft, no invariants) ---
    public string Name { get; set; } = "";
    public string RegisteredName { get; set; } = "";
    public string RegistrationNo { get; set; } = "";
    public string TaxId { get; set; } = "";
    public string Email { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string ContactPhone { get; set; } = "";
    public string Region { get; set; } = "Peninsular";
    public string State { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "Malaysia";

    /// <summary>SWEC category leaf/branch codes (conformed dimension; reuse the existing codes, §4).</summary>
    public List<string> Categories { get; set; } = [];

    /// <summary>Onboarding <c>FormTemplate</c> ids attached at invite (SPEC §5, D2).</summary>
    public List<Guid> SelectedTemplateIds { get; private set; } = [];

    // --- Owned staging collections (same value objects as the master, for a clean promotion copy) ---
    public List<VendorContact> Contacts { get; set; } = [];
    public List<VendorAddress> Addresses { get; set; } = [];
    public List<VendorBankAccount> BankAccounts { get; set; } = [];
    public List<VendorCertification> Certifications { get; set; } = [];

    /// <summary>Questionnaire answers, mirror of <c>BidAnswer</c> (FormTemplate + item order + value; D4).</summary>
    public List<OnboardingAnswer> Answers { get; set; } = [];

    /// <summary>Uploaded onboarding documents (SSM, ISO, bank letter, …), each backed by a <c>StoredFile</c>.</summary>
    public List<OnboardingDocument> Documents { get; set; } = [];

    /// <summary>Workflow-engine seam — ordered review steps (v1: 0 for manual, 1 for invite; §9).</summary>
    public List<OnboardingReviewStep> Steps { get; private set; } = [];

    /// <summary>Financial pre-qualification (Non-SWEC only; null when pre-qual is waived — SWEC, B1).</summary>
    public VendorFinancialAssessment? Financial { get; set; }

    /// <summary>Append-only clarification rounds (SPEC §4).</summary>
    public List<OnboardingClarificationRound> Rounds { get; private set; } = [];

    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }
    public DateTime? SubmittedUtc { get; private set; }
    public DateTime? DecisionUtc { get; private set; }

    /// <summary>The master vendor this application was promoted into on approval (A5).</summary>
    public Guid? PromotedVendorId { get; private set; }
    public string? RejectReason { get; private set; }

    // EF Core
    private VendorOnboardingApplication() { }

    /// <summary>
    /// Creates the staging application an invitation opens (A1/A2). Status <see cref="OnboardingStatus.Invited"/>,
    /// source SelfService, one workflow-seam review step (with a Finance sub-step flag for Non-SWEC, §9).
    /// The <paramref name="code"/> comes from <c>NumberSequence</c> (VOB-2026-####).
    /// </summary>
    public static VendorOnboardingApplication CreateFromInvitation(
        string code, VendorOnboardingInvitation invitation, DateTime nowUtc)
    {
        var app = new VendorOnboardingApplication
        {
            Code = code,
            Status = OnboardingStatus.Invited,
            Source = ApplicationSource.SelfService,
            Type = invitation.Type,
            Email = invitation.Email,
            InvitationId = invitation.Id,
            SelectedTemplateIds = [.. invitation.SelectedTemplateIds],
            CreatedUtc = nowUtc,
            UpdatedUtc = nowUtc,
        };
        app.Steps.Add(new OnboardingReviewStep(0, "Procurement review", requiresFinance: invitation.Type == VendorType.NonSwec));
        return app;
    }

    /// <summary>Vendor opens the link and starts filling the form (A2). Invited → InProgress.</summary>
    public OnboardingTransition MarkInProgress(DateTime nowUtc)
    {
        Require("start", OnboardingStatus.Invited);
        return To(OnboardingStatus.InProgress, "Onboarding started", null, nowUtc);
    }

    /// <summary>
    /// Vendor submits the completed form (A3). InProgress → Submitted; stamps <see cref="SubmittedUtc"/>
    /// and, when a financial assessment is present (Non-SWEC), captures the as-of submit snapshot (§6).
    /// </summary>
    public OnboardingTransition Submit(DateTime nowUtc)
    {
        Require("submit", OnboardingStatus.InProgress);
        SubmittedUtc = nowUtc;
        if (Type == VendorType.NonSwec)
            Financial?.CaptureSnapshot(FinancialSnapshotStage.AtSubmit, nowUtc);
        return To(OnboardingStatus.Submitted, "Application submitted", null, nowUtc);
    }

    /// <summary>Buyer picks the application up for review (A4). Submitted or Resubmitted → UnderReview.</summary>
    public OnboardingTransition StartReview(DateTime nowUtc)
    {
        Require("start review of", OnboardingStatus.Submitted, OnboardingStatus.Resubmitted);
        return To(OnboardingStatus.UnderReview, "Review started", null, nowUtc);
    }

    /// <summary>
    /// Buyer raises a batched clarification (A7): UnderReview → ClarificationRequested, appending one
    /// append-only round of N items. One round ⇒ one email.
    /// </summary>
    public OnboardingTransition RequestClarification(ClarificationDirection direction, string message,
        string byUserId, string byName, IEnumerable<OnboardingClarificationItem> items, DateTime nowUtc)
    {
        Require("request clarification on", OnboardingStatus.UnderReview);
        var round = new OnboardingClarificationRound(
            Id, Rounds.Count + 1, direction, message, byUserId, byName, items, nowUtc);
        Rounds.Add(round);
        return To(OnboardingStatus.ClarificationRequested, "Clarification requested",
            $"Round {round.RoundNo}: {round.Items.Count} item(s)", nowUtc);
    }

    /// <summary>
    /// Vendor raises its own clarification round back to the buyer (both directions, SPEC §4). Appends
    /// an append-only VendorToBuyer round without changing the application status. Allowed while the
    /// application is live (not terminal).
    /// </summary>
    public OnboardingClarificationRound RaiseVendorClarification(string message, string byName,
        IEnumerable<OnboardingClarificationItem> items, DateTime nowUtc)
    {
        RequireNonTerminal("raise a clarification on");
        var round = new OnboardingClarificationRound(
            Id, Rounds.Count + 1, ClarificationDirection.VendorToBuyer, message, "vendor", byName, items, nowUtc);
        Rounds.Add(round);
        UpdatedUtc = nowUtc;
        return round;
    }

    /// <summary>
    /// Vendor answers the open round and resubmits it whole (A8): ClarificationRequested → Resubmitted.
    /// Fills the responses on the latest open round (append-only). The buyer then re-reviews via
    /// <see cref="StartReview"/>.
    /// </summary>
    public OnboardingTransition Resubmit(IReadOnlyList<string> responses, DateTime nowUtc)
    {
        Require("resubmit", OnboardingStatus.ClarificationRequested);
        // Answer the buyer's open round specifically — a vendor-raised round may also be open.
        var open = Rounds.LastOrDefault(r => r.Status == ClarificationRoundStatus.Open && r.Direction == ClarificationDirection.BuyerToVendor)
            ?? throw new DomainRuleException("There is no open clarification round to resubmit.");
        open.Respond(responses, nowUtc);
        return To(OnboardingStatus.Resubmitted, "Clarification resubmitted",
            $"Round {open.RoundNo}", nowUtc);
    }

    /// <summary>
    /// Buyer approves (A5): UnderReview → Approved (terminal). Stamps the decision, records the master
    /// <paramref name="promotedVendorId"/>, captures the as-of decision financial snapshot (Non-SWEC),
    /// and completes the review step. Promotion into the master is performed by the caller (Slice D).
    /// </summary>
    public OnboardingTransition Approve(Guid promotedVendorId, string decidedByUserId, string decidedByName, DateTime nowUtc)
    {
        Require("approve", OnboardingStatus.UnderReview);
        PromotedVendorId = promotedVendorId;
        DecisionUtc = nowUtc;
        if (Type == VendorType.NonSwec)
            Financial?.CaptureSnapshot(FinancialSnapshotStage.AtDecision, nowUtc);
        RecordDecisionStep("Approved", decidedByUserId, decidedByName, null, nowUtc);
        return To(OnboardingStatus.Approved, "Application approved", null, nowUtc);
    }

    /// <summary>Buyer rejects with a reason (A6): UnderReview → Rejected (terminal). Master untouched.</summary>
    public OnboardingTransition Reject(string reason, string decidedByUserId, string decidedByName, DateTime nowUtc)
    {
        Require("reject", OnboardingStatus.UnderReview);
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainRuleException("A rejection reason is required.");
        RejectReason = reason;
        DecisionUtc = nowUtc;
        RecordDecisionStep("Rejected", decidedByUserId, decidedByName, reason, nowUtc);
        return To(OnboardingStatus.Rejected, "Application rejected", reason, nowUtc);
    }

    /// <summary>Buyer revokes the invite/application before a decision (A10). Non-terminal → Revoked.</summary>
    public OnboardingTransition Revoke(DateTime nowUtc)
    {
        RequireNonTerminal("revoke");
        return To(OnboardingStatus.Revoked, "Application revoked", null, nowUtc);
    }

    /// <summary>Link lapses before submit (A9). Invited/InProgress → Expired.</summary>
    public OnboardingTransition Expire(DateTime nowUtc)
    {
        Require("expire", OnboardingStatus.Invited, OnboardingStatus.InProgress);
        return To(OnboardingStatus.Expired, "Application expired", null, nowUtc);
    }

    /// <summary>Vendor withdraws before a decision. Non-terminal → Withdrawn.</summary>
    public OnboardingTransition Withdraw(DateTime nowUtc)
    {
        RequireNonTerminal("withdraw");
        return To(OnboardingStatus.Withdrawn, "Application withdrawn", null, nowUtc);
    }

    private void RecordDecisionStep(string outcome, string byUserId, string byName, string? reason, DateTime nowUtc)
    {
        var step = Steps.OrderBy(s => s.Order).FirstOrDefault(s => s.Outcome is null);
        step?.Decide(outcome, byUserId, byName, reason, nowUtc);
    }

    private static readonly OnboardingStatus[] Terminal =
        [OnboardingStatus.Approved, OnboardingStatus.Rejected, OnboardingStatus.Expired,
         OnboardingStatus.Revoked, OnboardingStatus.Withdrawn];

    private void Require(string action, params OnboardingStatus[] allowed)
    {
        if (!allowed.Contains(Status))
            throw new DomainRuleException(
                $"Cannot {action} an application that is {Status}. Expected: {string.Join(" or ", allowed)}.");
    }

    private void RequireNonTerminal(string action)
    {
        if (Terminal.Contains(Status))
            throw new DomainRuleException($"Cannot {action} a {Status} application (terminal).");
    }

    private OnboardingTransition To(OnboardingStatus to, string action, string? reason, DateTime nowUtc)
    {
        var from = Status;
        Status = to;
        UpdatedUtc = nowUtc;
        return new OnboardingTransition(from, to, action, reason);
    }
}

/// <summary>One questionnaire answer — mirror of <c>BidAnswer</c> (D4): identifies the item by its
/// template + order and stores the value, so onboarding and RFQ answers stay analytically consistent.</summary>
public class OnboardingAnswer
{
    public Guid FormTemplateId { get; set; }
    public int QuestionOrder { get; set; }        // aligns to FormTemplate FormItem.Order
    public string Value { get; set; } = "";
}

/// <summary>An uploaded onboarding document (grain: one document on one application). The bytes live
/// in a <c>StoredFile</c>; this holds the checklist key (ssm/iso/bank/…), the file name, and the id.</summary>
public class OnboardingDocument
{
    public string Key { get; set; } = "";
    public string FileName { get; set; } = "";
    public Guid StoredFileId { get; set; }
    public DateTime UploadedUtc { get; set; }
}

/// <summary>
/// A workflow-seam review step (§9). v1 has one step for an invited application; a future
/// configurable engine inserts more without reshaping tables. Each decision carries actor +
/// timestamp + outcome + reason. <see cref="RequiresFinance"/> flags the Non-SWEC finance sub-step.
/// </summary>
public class OnboardingReviewStep
{
    public int Order { get; set; }
    public string Name { get; set; } = "";
    public bool RequiresFinance { get; set; }
    public string? Outcome { get; private set; }
    public string? DecidedByUserId { get; private set; }
    public string? DecidedByName { get; private set; }
    public DateTime? DecidedUtc { get; private set; }
    public string? Reason { get; private set; }

    public OnboardingReviewStep() { }

    public OnboardingReviewStep(int order, string name, bool requiresFinance)
    {
        Order = order;
        Name = name;
        RequiresFinance = requiresFinance;
    }

    internal void Decide(string outcome, string byUserId, string byName, string? reason, DateTime nowUtc)
    {
        Outcome = outcome;
        DecidedByUserId = byUserId;
        DecidedByName = byName;
        Reason = reason;
        DecidedUtc = nowUtc;
    }
}

/// <summary>The recorded result of an application transition — the single signal a service turns
/// into a typed <c>AuditEntry</c> (from/to/reason) for onboarding cycle-time analytics.</summary>
public sealed record OnboardingTransition(
    OnboardingStatus From, OnboardingStatus To, string Action, string? Reason);
