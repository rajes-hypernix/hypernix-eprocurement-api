namespace eProcure.Application.Sourcing;

/// <summary>
/// Configurable governance limits for RFQ invitations/extensions (RFQ-LIFECYCLE-ADDENDUM §3, G2/G5).
/// Bound from the "RfqGovernance" configuration section; defaults match the spec.
/// </summary>
public sealed class RfqGovernanceOptions
{
    /// <summary>Maximum forward extensions of an RFQ deadline (G5). Default 2.</summary>
    public int MaxExtensions { get; set; } = 2;

    /// <summary>Minimum hours that must remain before close for a live (Open-RFQ) invite (G2); inside
    /// this window the buyer must extend first. Default 72.</summary>
    public int MinRemainingHoursForLateInvite { get; set; } = 72;
}
