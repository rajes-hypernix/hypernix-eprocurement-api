namespace FSH.Modules.Suppliers.Domain.Onboarding;

/// <summary>
/// One clarification exchange on an application — one packet, one email (grain: one round).
/// Append-only: once raised, the round and its items are immutable except that the response
/// side is filled once (Open -&gt; Responded). Directional so either party can batch several
/// points into a single round.
/// </summary>
public sealed class OnboardingClarificationRound
{
    private readonly List<OnboardingClarificationItem> _items = [];

    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }

    /// <summary>1-based sequence within the application (round 1, 2, ...).</summary>
    public int RoundNo { get; private set; }

    public ClarificationDirection Direction { get; private set; }
    public ClarificationRoundStatus Status { get; private set; } = ClarificationRoundStatus.Open;
    public string Message { get; private set; } = string.Empty;
    public string RaisedByUserId { get; private set; } = default!;
    public string RaisedByName { get; private set; } = default!;
    public DateTime RaisedUtc { get; private set; }
    public DateTime? RespondedUtc { get; private set; }

    public IReadOnlyList<OnboardingClarificationItem> Items => _items;

    private OnboardingClarificationRound() { }

    public OnboardingClarificationRound(
        Guid applicationId,
        int roundNo,
        ClarificationDirection direction,
        string message,
        string raisedByUserId,
        string raisedByName,
        IEnumerable<OnboardingClarificationItem> items,
        DateTime nowUtc)
    {
        if (roundNo < 1)
        {
            throw new OnboardingRuleException("A clarification round number starts at 1.");
        }

        var list = items.ToList();
        if (list.Count == 0)
        {
            throw new OnboardingRuleException("A clarification round needs at least one item.");
        }

        Id = Guid.CreateVersion7();
        ApplicationId = applicationId;
        RoundNo = roundNo;
        Direction = direction;
        Message = message;
        RaisedByUserId = raisedByUserId;
        RaisedByName = raisedByName;
        _items.AddRange(list);
        RaisedUtc = nowUtc;
    }

    /// <summary>
    /// Records the responses to this round's items and closes it (Open -&gt; Responded). Responses
    /// align to items by order; the request side is never mutated.
    /// </summary>
    public void Respond(IReadOnlyList<string> responses, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(responses);

        if (Status != ClarificationRoundStatus.Open)
        {
            throw new OnboardingRuleException($"Clarification round {RoundNo} is already {Status}.");
        }

        if (responses.Count != _items.Count)
        {
            throw new OnboardingRuleException("A response is required for every clarification item.");
        }

        for (int i = 0; i < _items.Count; i++)
        {
            _items[i].SetResponse(responses[i]);
        }

        Status = ClarificationRoundStatus.Responded;
        RespondedUtc = nowUtc;
    }
}
