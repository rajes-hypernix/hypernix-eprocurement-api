namespace eProcure.Domain.Onboarding;

/// <summary>
/// Vendor-onboarding application lifecycle (VENDOR-ONBOARDING-SPEC §2). Stored as a stable string
/// token (DATA-MODEL-ANALYTICS §4/§5) so it round-trips for cycle-time analytics. Terminal states:
/// <see cref="Approved"/>, <see cref="Rejected"/>, <see cref="Expired"/>, <see cref="Revoked"/>,
/// <see cref="Withdrawn"/>.
/// </summary>
public enum OnboardingStatus
{
    Draft,                    // manual, unsent (v1 manual entry — seam only)
    Invited,                  // invite sent; magic link live
    InProgress,               // vendor opened the link and is filling the form
    Submitted,                // vendor submitted; financial snapshot captured
    UnderReview,              // buyer picked it up
    ClarificationRequested,   // buyer raised a round; waiting on the vendor
    Resubmitted,              // vendor answered the round; back with the buyer
    Approved,                 // promoted to the Vendor master
    Rejected,                 // terminal, reason recorded
    Expired,                  // link expired before submit
    Revoked,                  // invite withdrawn by the buyer
    Withdrawn,                // withdrawn by the vendor
}

/// <summary>How an application entered the system — a conformed dimension (DATA-MODEL-ANALYTICS §4).</summary>
public enum ApplicationSource { Manual, SelfService }

/// <summary>Direction of a clarification round (SPEC §4): the buyer or the vendor may open one.</summary>
public enum ClarificationDirection { BuyerToVendor, VendorToBuyer }

/// <summary>
/// Status of a clarification round. Append-only (DATA-MODEL-ANALYTICS §2): a sent round is
/// immutable; the response fills a separate field and flips it <see cref="Open"/> → <see cref="Responded"/>.
/// (The prototype's round.status. Named distinctly from the sourcing <c>LinkStatus</c> to avoid a clash.)
/// </summary>
public enum ClarificationRoundStatus { Open, Responded }

/// <summary>
/// Magic-link invitation lifecycle (DATA-MODEL-ANALYTICS §10). The token itself is stored hashed;
/// this tracks the invite's own state independently of the application it opens.
/// </summary>
public enum OnboardingInvitationStatus { Sent, Opened, Expired, Revoked, Completed }

/// <summary>Altman Z′ private-firm band (SPEC §3): A (sound) … D (distress).</summary>
public enum FinancialBand { A, B, C, D }

/// <summary>Risk category derived from the financial band (SPEC §3).</summary>
public enum RiskCategory { Low, LowMedium, Medium, High }

/// <summary>Which decision point an as-of financial snapshot was captured at (DATA-MODEL-ANALYTICS §6).</summary>
public enum FinancialSnapshotStage { AtSubmit, AtDecision }
