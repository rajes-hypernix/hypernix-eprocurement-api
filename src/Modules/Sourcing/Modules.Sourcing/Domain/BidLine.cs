namespace FSH.Modules.Sourcing.Domain;

/// <summary>One vendor-quoted line on a <see cref="Bid"/>, keyed to an RFQ line by ItemCode (no FK — same-aggregate free text, matching the old system).</summary>
public sealed class BidLine
{
    public string ItemCode { get; private set; }
    public bool Bidding { get; private set; }
    public decimal Price { get; private set; }
    public decimal Qty { get; private set; }
    public bool Partial { get; private set; }
    public string? AltItem { get; private set; }

    public BidLine(string itemCode, bool bidding, decimal price, decimal qty, bool partial, string? altItem)
    {
        ItemCode = itemCode;
        Bidding = bidding;
        Price = price;
        Qty = qty;
        Partial = partial;
        AltItem = altItem;
    }
}
