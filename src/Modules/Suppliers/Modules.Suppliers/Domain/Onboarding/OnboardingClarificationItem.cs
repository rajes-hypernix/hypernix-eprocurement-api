namespace FSH.Modules.Suppliers.Domain.Onboarding;

/// <summary>
/// One flagged item within a clarification round. The request side (<see cref="Topic"/>,
/// <see cref="Request"/>) is immutable once raised; <see cref="Response"/> is filled exactly once
/// when the round is answered.
/// </summary>
public sealed class OnboardingClarificationItem
{
    public Guid Id { get; private set; }
    public string Topic { get; private set; }
    public string Request { get; private set; }
    public string Response { get; private set; } = string.Empty;

    public OnboardingClarificationItem(string topic, string request)
    {
        Id = Guid.CreateVersion7();
        Topic = topic;
        Request = request;
    }

    internal void SetResponse(string response) => Response = response;
}
