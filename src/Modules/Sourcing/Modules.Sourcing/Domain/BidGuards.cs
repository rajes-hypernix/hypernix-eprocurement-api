namespace FSH.Modules.Sourcing.Domain;

/// <summary>
/// Shared bid-deadline guard for save-draft/submit — matches the old system's
/// <c>BidService.EnsureOpen</c> rule. Lives outside <see cref="Rfq"/> because <see cref="Bid"/>
/// is a separate aggregate and can't call Rfq's private guards directly.
/// </summary>
public static class BidGuards
{
    public static void EnsureOpen(Rfq rfq, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(rfq);

        if (rfq.Status != RfqStatus.Open || (rfq.ClosesUtc is { } closes && nowUtc > closes))
        {
            throw new SourcingRuleException($"Cannot bid — {rfq.Code} is not open for bidding.");
        }
    }
}
