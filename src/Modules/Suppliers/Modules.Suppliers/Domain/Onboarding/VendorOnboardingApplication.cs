using FSH.Framework.Core.Domain;

namespace FSH.Modules.Suppliers.Domain.Onboarding;

/// <summary>
/// A vendor-onboarding application — the aggregate root (grain: one application). This is the
/// staging record: everything the vendor types lives here and never touches the <see cref="Vendor"/>
/// master until <see cref="Approve"/> promotes it. Mirrors the master's shape (profile scalars +
/// owned contacts/addresses/banking/certs) so promotion is a copy.
///
/// Every lifecycle move is a method here, throws <see cref="OnboardingRuleException"/> on an
/// illegal transition, stamps time via the supplied UTC, and returns an
/// <see cref="OnboardingTransition"/> the calling handler records for auditing.
/// </summary>
public sealed class VendorOnboardingApplication : AggregateRoot<Guid>
{
    private static readonly OnboardingStatus[] Terminal =
    [
        OnboardingStatus.Approved, OnboardingStatus.Rejected, OnboardingStatus.Expired,
        OnboardingStatus.Revoked, OnboardingStatus.Withdrawn,
    ];

    private readonly List<VendorContact> _contacts = [];
    private readonly List<VendorAddress> _addresses = [];
    private readonly List<VendorBankAccount> _bankAccounts = [];
    private readonly List<VendorCertification> _certifications = [];
    private readonly List<OnboardingAnswer> _answers = [];
    private readonly List<OnboardingDocument> _documents = [];
    private readonly List<OnboardingReviewStep> _steps = [];
    private readonly List<OnboardingClarificationRound> _rounds = [];

    /// <summary>VOB-2026-0007 — business key, immutable.</summary>
    public string Code { get; private set; } = default!;

    public OnboardingStatus Status { get; private set; } = OnboardingStatus.Invited;
    public ApplicationSource Source { get; private set; } = ApplicationSource.SelfService;

    /// <summary>"Swec" or "NonSwec" — set at creation; drives the financial waiver + promoted status.</summary>
    public string Type { get; private set; } = "NonSwec";

    /// <summary>The invitation that opened this application (null for manual entry).</summary>
    public Guid? InvitationId { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string RegisteredName { get; private set; } = string.Empty;
    public string RegistrationNo { get; private set; } = string.Empty;
    public string TaxId { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string ContactName { get; private set; } = string.Empty;
    public string ContactPhone { get; private set; } = string.Empty;
    public string Region { get; private set; } = "Peninsular";
    public string State { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string Country { get; private set; } = "Malaysia";

    /// <summary>SWEC category leaf/branch codes.</summary>
    public List<string> Categories { get; private set; } = [];

    /// <summary>Onboarding FormTemplate ids attached at invite (opaque — no real Forms module yet).</summary>
    public List<Guid> SelectedTemplateIds { get; private set; } = [];

    public VendorFinancialAssessment? Financial { get; private set; }

    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }
    public DateTime? SubmittedUtc { get; private set; }
    public DateTime? DecisionUtc { get; private set; }

    /// <summary>The master vendor this application was promoted into on approval.</summary>
    public Guid? PromotedVendorId { get; private set; }
    public string? RejectReason { get; private set; }

    public IReadOnlyList<VendorContact> Contacts => _contacts;
    public IReadOnlyList<VendorAddress> Addresses => _addresses;
    public IReadOnlyList<VendorBankAccount> BankAccounts => _bankAccounts;
    public IReadOnlyList<VendorCertification> Certifications => _certifications;
    public IReadOnlyList<OnboardingAnswer> Answers => _answers;
    public IReadOnlyList<OnboardingDocument> Documents => _documents;
    public IReadOnlyList<OnboardingReviewStep> Steps => _steps;
    public IReadOnlyList<OnboardingClarificationRound> Rounds => _rounds;

    private VendorOnboardingApplication() { }

    /// <summary>
    /// Creates the staging application an invitation opens. Status Invited, source SelfService, one
    /// workflow-seam review step (with a Finance sub-step flag for Non-SWEC).
    /// </summary>
    public static VendorOnboardingApplication CreateFromInvitation(string code, VendorOnboardingInvitation invitation, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        var app = new VendorOnboardingApplication
        {
            Id = Guid.CreateVersion7(),
            Code = code,
            Status = OnboardingStatus.Invited,
            Source = ApplicationSource.SelfService,
            Type = invitation.Type,
            Email = invitation.Email,
            InvitationId = invitation.Id,
            CreatedUtc = nowUtc,
            UpdatedUtc = nowUtc,
        };
        app.SelectedTemplateIds.AddRange(invitation.SelectedTemplateIds);
        app._steps.Add(new OnboardingReviewStep(0, "Procurement review", requiresFinance: invitation.Type == "NonSwec"));
        return app;
    }

    /// <summary>Vendor opens the link and starts filling the form. Invited -&gt; InProgress.</summary>
    public OnboardingTransition MarkInProgress(DateTime nowUtc)
    {
        Require("start", OnboardingStatus.Invited);
        return To(OnboardingStatus.InProgress, "Onboarding started", null, nowUtc);
    }

    /// <summary>
    /// Partial-patch draft save. Only non-null fields are applied. Only allowed while
    /// Invited/InProgress (<see cref="RequireDraftable"/>).
    /// </summary>
    public void SaveDraft(
        string? name,
        string? registeredName,
        string? registrationNo,
        string? taxId,
        string? email,
        string? contactName,
        string? contactPhone,
        string? region,
        string? state,
        string? city,
        string? country,
        IReadOnlyList<string>? categories,
        DateTime nowUtc)
    {
        RequireDraftable();

        if (name is not null)
        {
            Name = name;
        }

        if (registeredName is not null)
        {
            RegisteredName = registeredName;
        }

        if (registrationNo is not null)
        {
            RegistrationNo = registrationNo;
        }

        if (taxId is not null)
        {
            TaxId = taxId;
        }

        if (email is not null)
        {
            Email = email;
        }

        if (contactName is not null)
        {
            ContactName = contactName;
        }

        if (contactPhone is not null)
        {
            ContactPhone = contactPhone;
        }

        if (region is not null)
        {
            Region = region;
        }

        if (state is not null)
        {
            State = state;
        }

        if (city is not null)
        {
            City = city;
        }

        if (country is not null)
        {
            Country = country;
        }

        if (categories is not null)
        {
            Categories = [.. categories.Distinct()];
        }

        UpdatedUtc = nowUtc;
    }

    public void ReplaceContacts(IEnumerable<VendorContact> contacts)
    {
        RequireDraftable();
        _contacts.Clear();
        _contacts.AddRange(contacts);
        UpdatedUtc = DateTime.UtcNow;
    }

    public void ReplaceAddresses(IEnumerable<VendorAddress> addresses)
    {
        RequireDraftable();
        _addresses.Clear();
        _addresses.AddRange(addresses);
        UpdatedUtc = DateTime.UtcNow;
    }

    public void ReplaceBankAccounts(IEnumerable<VendorBankAccount> bankAccounts)
    {
        RequireDraftable();
        _bankAccounts.Clear();
        _bankAccounts.AddRange(bankAccounts);
        UpdatedUtc = DateTime.UtcNow;
    }

    public void ReplaceCertifications(IEnumerable<VendorCertification> certifications)
    {
        RequireDraftable();
        _certifications.Clear();
        _certifications.AddRange(certifications);
        UpdatedUtc = DateTime.UtcNow;
    }

    public void ReplaceAnswers(IEnumerable<OnboardingAnswer> answers)
    {
        RequireDraftable();
        _answers.Clear();
        _answers.AddRange(answers);
        UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>Non-SWEC only — attaches or replaces the financial assessment while still draftable.</summary>
    public void AttachFinancial(VendorFinancialAssessment financial)
    {
        ArgumentNullException.ThrowIfNull(financial);
        RequireDraftable();
        Financial = financial;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void AddOrReplaceDocument(OnboardingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        RequireDraftable();
        _documents.RemoveAll(d => d.Key == document.Key);
        _documents.Add(document);
        UpdatedUtc = DateTime.UtcNow;
    }

    public void RemoveDocument(string key)
    {
        RequireDraftable();
        _documents.RemoveAll(d => d.Key == key);
        UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>Only Invited/InProgress applications are editable.</summary>
    private void RequireDraftable()
    {
        if (Status is not (OnboardingStatus.Invited or OnboardingStatus.InProgress))
        {
            throw new OnboardingRuleException($"This application is {Status} and can no longer be edited.");
        }
    }

    /// <summary>
    /// Vendor submits the completed form. InProgress -&gt; Submitted; stamps <see cref="SubmittedUtc"/>
    /// and, when a financial assessment is present (Non-SWEC), captures the as-of submit snapshot.
    /// </summary>
    public OnboardingTransition Submit(DateTime nowUtc)
    {
        Require("submit", OnboardingStatus.InProgress);

        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(RegistrationNo))
        {
            throw new OnboardingRuleException("Complete the required company fields (registered name and SSM number) before submitting.");
        }

        if (Type == "NonSwec")
        {
            if (Financial is null || Financial.Years.Count == 0)
            {
                throw new OnboardingRuleException("Non-SWEC applications require three years of financial figures before submitting.");
            }

            if (Financial.Years.Any(y => y.TotalAssets <= 0 || y.TotalLiabilities <= 0))
            {
                throw new OnboardingRuleException("Each financial year needs positive total assets and total liabilities.");
            }
        }

        SubmittedUtc = nowUtc;
        if (Type == "NonSwec")
        {
            Financial?.CaptureSnapshot(FinancialSnapshotStage.AtSubmit, nowUtc);
        }

        return To(OnboardingStatus.Submitted, "Application submitted", null, nowUtc);
    }

    /// <summary>Buyer picks the application up for review. Submitted or Resubmitted -&gt; UnderReview.</summary>
    public OnboardingTransition StartReview(DateTime nowUtc)
    {
        Require("start review of", OnboardingStatus.Submitted, OnboardingStatus.Resubmitted);
        return To(OnboardingStatus.UnderReview, "Review started", null, nowUtc);
    }

    /// <summary>
    /// Buyer raises a batched clarification: UnderReview -&gt; ClarificationRequested, appending one
    /// append-only round of N items.
    /// </summary>
    public OnboardingTransition RequestClarification(
        ClarificationDirection direction, string message, string byUserId, string byName,
        IEnumerable<OnboardingClarificationItem> items, DateTime nowUtc)
    {
        Require("request clarification on", OnboardingStatus.UnderReview);
        var round = new OnboardingClarificationRound(Id, _rounds.Count + 1, direction, message, byUserId, byName, items, nowUtc);
        _rounds.Add(round);
        return To(OnboardingStatus.ClarificationRequested, "Clarification requested",
            $"Round {round.RoundNo}: {round.Items.Count} item(s)", nowUtc);
    }

    /// <summary>
    /// Vendor raises its own clarification round back to the buyer. Appends an append-only
    /// VendorToBuyer round without changing the application status. Allowed while non-terminal.
    /// </summary>
    public OnboardingClarificationRound RaiseVendorClarification(string message, string byName, IEnumerable<OnboardingClarificationItem> items, DateTime nowUtc)
    {
        RequireNonTerminal("raise a clarification on");
        var round = new OnboardingClarificationRound(Id, _rounds.Count + 1, ClarificationDirection.VendorToBuyer, message, "vendor", byName, items, nowUtc);
        _rounds.Add(round);
        UpdatedUtc = nowUtc;
        return round;
    }

    /// <summary>
    /// Vendor answers the open round and resubmits it whole: ClarificationRequested -&gt; Resubmitted.
    /// Fills the responses on the latest open buyer-raised round.
    /// </summary>
    public OnboardingTransition Resubmit(IReadOnlyList<string> responses, DateTime nowUtc)
    {
        Require("resubmit", OnboardingStatus.ClarificationRequested);
        var open = _rounds.LastOrDefault(r => r.Status == ClarificationRoundStatus.Open && r.Direction == ClarificationDirection.BuyerToVendor)
            ?? throw new OnboardingRuleException("There is no open clarification round to resubmit.");
        open.Respond(responses, nowUtc);
        return To(OnboardingStatus.Resubmitted, "Clarification resubmitted", $"Round {open.RoundNo}", nowUtc);
    }

    /// <summary>
    /// Buyer approves: UnderReview -&gt; Approved (terminal). Stamps the decision, records the master
    /// <paramref name="promotedVendorId"/>, captures the as-of decision financial snapshot (Non-SWEC),
    /// and completes the review step. Promotion into the master is performed by the caller.
    /// </summary>
    public OnboardingTransition Approve(Guid promotedVendorId, string decidedByUserId, string decidedByName, DateTime nowUtc)
    {
        Require("approve", OnboardingStatus.UnderReview);

        if (Type == "NonSwec" && (Financial is null || Financial.Years.Count == 0))
        {
            throw new OnboardingRuleException("A Non-SWEC application cannot be approved without a completed financial assessment.");
        }

        PromotedVendorId = promotedVendorId;
        DecisionUtc = nowUtc;
        if (Type == "NonSwec")
        {
            Financial?.CaptureSnapshot(FinancialSnapshotStage.AtDecision, nowUtc);
        }

        RecordDecisionStep("Approved", decidedByUserId, decidedByName, null, nowUtc);
        return To(OnboardingStatus.Approved, "Application approved", null, nowUtc);
    }

    /// <summary>Buyer rejects with a reason: UnderReview -&gt; Rejected (terminal). Master untouched.</summary>
    public OnboardingTransition Reject(string reason, string decidedByUserId, string decidedByName, DateTime nowUtc)
    {
        Require("reject", OnboardingStatus.UnderReview);
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new OnboardingRuleException("A rejection reason is required.");
        }

        RejectReason = reason;
        DecisionUtc = nowUtc;
        RecordDecisionStep("Rejected", decidedByUserId, decidedByName, reason, nowUtc);
        return To(OnboardingStatus.Rejected, "Application rejected", reason, nowUtc);
    }

    /// <summary>Buyer revokes the invite/application before a decision. Non-terminal -&gt; Revoked.</summary>
    public OnboardingTransition Revoke(DateTime nowUtc)
    {
        RequireNonTerminal("revoke");
        return To(OnboardingStatus.Revoked, "Application revoked", null, nowUtc);
    }

    /// <summary>Vendor withdraws before a decision. Non-terminal -&gt; Withdrawn.</summary>
    public OnboardingTransition Withdraw(DateTime nowUtc)
    {
        RequireNonTerminal("withdraw");
        return To(OnboardingStatus.Withdrawn, "Application withdrawn", null, nowUtc);
    }

    private void RecordDecisionStep(string outcome, string byUserId, string byName, string? reason, DateTime nowUtc)
    {
        var step = _steps.OrderBy(s => s.Order).FirstOrDefault(s => s.Outcome is null);
        step?.Decide(outcome, byUserId, byName, reason, nowUtc);
    }

    private void Require(string action, params OnboardingStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new OnboardingRuleException($"Cannot {action} an application that is {Status}. Expected: {string.Join(" or ", allowed)}.");
        }
    }

    private void RequireNonTerminal(string action)
    {
        if (Terminal.Contains(Status))
        {
            throw new OnboardingRuleException($"Cannot {action} a {Status} application (terminal).");
        }
    }

    private OnboardingTransition To(OnboardingStatus to, string action, string? reason, DateTime nowUtc)
    {
        var from = Status;
        Status = to;
        UpdatedUtc = nowUtc;
        return new OnboardingTransition(from, to, action, reason);
    }
}

/// <summary>The recorded result of an application transition.</summary>
public sealed record OnboardingTransition(OnboardingStatus From, OnboardingStatus To, string Action, string? Reason);
