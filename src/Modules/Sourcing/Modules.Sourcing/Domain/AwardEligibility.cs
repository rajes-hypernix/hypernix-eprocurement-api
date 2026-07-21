namespace FSH.Modules.Sourcing.Domain;

/// <summary>
/// Award eligibility: a vendor must have submitted, and — for Dual-envelope RFQs whose technical
/// evaluation has been finalized — must have technically passed. Single-envelope RFQs and
/// not-yet-finalized Dual RFQs have no technical gate yet.
/// </summary>
public static class AwardEligibility
{
    public static bool IsEligible(Rfq rfq, bool submitted, bool technicallyPassed)
    {
        ArgumentNullException.ThrowIfNull(rfq);

        if (!submitted)
        {
            return false;
        }

        return rfq.Envelope == RfqEnvelope.Single || !rfq.TechFinalized || technicallyPassed;
    }
}
