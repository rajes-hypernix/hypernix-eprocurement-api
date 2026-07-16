namespace FSH.Modules.Suppliers.Domain.Onboarding;

/// <summary>
/// A workflow-seam review step. v1 has one step for an invited application; a future configurable
/// engine could insert more without reshaping tables. <see cref="RequiresFinance"/> flags the
/// Non-SWEC finance sub-step.
/// </summary>
public sealed class OnboardingReviewStep
{
    public int Order { get; private set; }
    public string Name { get; private set; }
    public bool RequiresFinance { get; private set; }
    public string? Outcome { get; private set; }
    public string? DecidedByUserId { get; private set; }
    public string? DecidedByName { get; private set; }
    public DateTime? DecidedUtc { get; private set; }
    public string? Reason { get; private set; }

    public OnboardingReviewStep(int order, string name, bool requiresFinance)
    {
        Order = order;
        Name = name;
        RequiresFinance = requiresFinance;
    }

    internal void Decide(string outcome, string byUserId, string byName, string? reason, DateTime nowUtc)
    {
        Outcome = outcome;
        DecidedByUserId = byUserId;
        DecidedByName = byName;
        Reason = reason;
        DecidedUtc = nowUtc;
    }
}
