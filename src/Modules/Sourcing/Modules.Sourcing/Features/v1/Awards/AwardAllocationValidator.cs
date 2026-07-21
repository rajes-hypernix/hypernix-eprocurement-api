using FSH.Modules.Sourcing.Domain;

namespace FSH.Modules.Sourcing.Features.v1.Awards;

/// <summary>
/// Cross-aggregate allocation caps — spans Rfq (required qty per line) and Bid (offered qty per
/// vendor per line), which is why this validation lives at the handler level rather than inside
/// either aggregate.
/// </summary>
internal static class AwardAllocationValidator
{
    internal static void Validate(Rfq rfq, IReadOnlyDictionary<Guid, Bid> bidsByVendor, IReadOnlyList<AwardAllocation> allocations)
    {
        ArgumentNullException.ThrowIfNull(rfq);
        ArgumentNullException.ThrowIfNull(bidsByVendor);
        ArgumentNullException.ThrowIfNull(allocations);

        var linesByCode = rfq.Lines.ToDictionary(l => l.LineCode);
        var sumByLine = new Dictionary<string, decimal>();

        foreach (var allocation in allocations)
        {
            if (!linesByCode.TryGetValue(allocation.RfqLineCode, out var rfqLine))
            {
                throw new SourcingRuleException($"RFQ line {allocation.RfqLineCode} does not exist.");
            }

            if (!bidsByVendor.TryGetValue(allocation.VendorId, out var bid))
            {
                throw new SourcingRuleException("Cannot allocate to a vendor with no bid on this RFQ.");
            }

            var offeredLine = bid.Lines.FirstOrDefault(l => l.ItemCode == rfqLine.ItemCode && l.Bidding);
            if (offeredLine is null)
            {
                throw new SourcingRuleException($"Vendor did not bid on line {allocation.RfqLineCode}.");
            }

            decimal cap = Math.Min(rfqLine.Qty, offeredLine.Qty);
            if (allocation.Qty > cap)
            {
                throw new SourcingRuleException($"Allocated quantity for line {allocation.RfqLineCode} exceeds the required/offered quantity.");
            }

            sumByLine[allocation.RfqLineCode] = sumByLine.GetValueOrDefault(allocation.RfqLineCode) + allocation.Qty;
        }

        foreach (var (lineCode, sum) in sumByLine)
        {
            if (sum > linesByCode[lineCode].Qty)
            {
                throw new SourcingRuleException($"Total allocated quantity for line {lineCode} exceeds the required quantity.");
            }
        }
    }
}
