namespace FSH.Modules.Suppliers.Domain.Onboarding;

/// <summary>
/// Vendor-onboarding application lifecycle. Terminal states: <see cref="Approved"/>,
/// <see cref="Rejected"/>, <see cref="Expired"/>, <see cref="Revoked"/>, <see cref="Withdrawn"/>.
/// </summary>
public enum OnboardingStatus
{
    Draft,
    Invited,
    InProgress,
    Submitted,
    UnderReview,
    ClarificationRequested,
    Resubmitted,
    Approved,
    Rejected,
    Expired,
    Revoked,
    Withdrawn,
}

public enum ApplicationSource { Manual, SelfService }

public enum ClarificationDirection { BuyerToVendor, VendorToBuyer }

public enum ClarificationRoundStatus { Open, Responded }

public enum OnboardingInvitationStatus { Sent, Opened, Expired, Revoked, Completed }

/// <summary>Altman Z′ private-firm band: A (sound) … D (distress).</summary>
public enum FinancialBand { A, B, C, D }

public enum RiskCategory { Low, LowMedium, Medium, High }

public enum FinancialSnapshotStage { AtSubmit, AtDecision }
