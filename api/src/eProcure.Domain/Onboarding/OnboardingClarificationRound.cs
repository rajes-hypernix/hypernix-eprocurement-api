namespace eProcure.Domain.Onboarding;

/// <summary>
/// One clarification exchange on an application — one packet, one email (grain: one round; SPEC §4,
/// DATA-MODEL-ANALYTICS §7). <b>Append-only</b>: once raised, the round and its items are immutable
/// except that the response side is filled once (Open → Responded). Directional so either party can
/// batch several points into a single round. Yields round-count / cycle-time analytics for free.
/// </summary>
public class OnboardingClarificationRound
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Owning application (FK; indexed).</summary>
    public Guid ApplicationId { get; private set; }

    /// <summary>1-based sequence within the application (round 1, 2, …).</summary>
    public int RoundNo { get; private set; }

    public ClarificationDirection Direction { get; private set; }
    public ClarificationRoundStatus Status { get; private set; } = ClarificationRoundStatus.Open;

    /// <summary>Covering message sent with the round (the single email body).</summary>
    public string Message { get; private set; } = "";

    public string RaisedByUserId { get; private set; } = default!;
    public string RaisedByName { get; private set; } = default!;
    public DateTime RaisedUtc { get; private set; }
    public DateTime? RespondedUtc { get; private set; }

    /// <summary>The flagged items in this round (append-only; each response filled once).</summary>
    public List<OnboardingClarificationItem> Items { get; private set; } = [];

    // EF Core
    private OnboardingClarificationRound() { }

    public OnboardingClarificationRound(Guid applicationId, int roundNo, ClarificationDirection direction,
        string message, string raisedByUserId, string raisedByName,
        IEnumerable<OnboardingClarificationItem> items, DateTime nowUtc)
    {
        if (roundNo < 1) throw new DomainRuleException("A clarification round number starts at 1.");
        var list = items.ToList();
        if (list.Count == 0) throw new DomainRuleException("A clarification round needs at least one item.");

        ApplicationId = applicationId;
        RoundNo = roundNo;
        Direction = direction;
        Message = message;
        RaisedByUserId = raisedByUserId;
        RaisedByName = raisedByName;
        Items = list;
        RaisedUtc = nowUtc;
    }

    /// <summary>
    /// Records the responses to this round's items and closes it (Open → Responded). Responses align
    /// to items by order; the request side is never mutated (append-only, F4). Illegal if already
    /// responded or if the count doesn't match.
    /// </summary>
    public void Respond(IReadOnlyList<string> responses, DateTime nowUtc)
    {
        if (Status != ClarificationRoundStatus.Open)
            throw new DomainRuleException($"Clarification round {RoundNo} is already {Status}.");
        if (responses.Count != Items.Count)
            throw new DomainRuleException("A response is required for every clarification item.");

        for (var i = 0; i < Items.Count; i++) Items[i].SetResponse(responses[i]);
        Status = ClarificationRoundStatus.Responded;
        RespondedUtc = nowUtc;
    }
}

/// <summary>
/// One flagged item within a clarification round (grain: one item; §7). The request side
/// (<see cref="Topic"/>, <see cref="Request"/>) is immutable once raised; <see cref="Response"/> is
/// filled exactly once when the round is answered (append-only, F4).
/// </summary>
public class OnboardingClarificationItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Topic { get; private set; } = default!;
    public string Request { get; private set; } = default!;
    public string Response { get; private set; } = "";

    // EF Core
    private OnboardingClarificationItem() { }

    public OnboardingClarificationItem(string topic, string request)
    {
        Topic = topic;
        Request = request;
    }

    internal void SetResponse(string response) => Response = response;
}
