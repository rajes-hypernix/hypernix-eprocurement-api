namespace FSH.Modules.Sourcing.Domain;

/// <summary>Aligns to <c>Rfq.FormItems[].Order</c> by position — no FK, matches the old system's positional binding.</summary>
public sealed class BidAnswer
{
    public int QuestionOrder { get; private set; }
    public string Value { get; private set; }

    public BidAnswer(int questionOrder, string value)
    {
        QuestionOrder = questionOrder;
        Value = value;
    }
}
