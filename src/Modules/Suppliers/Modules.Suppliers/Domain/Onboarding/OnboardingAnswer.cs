namespace FSH.Modules.Suppliers.Domain.Onboarding;

/// <summary>
/// One questionnaire answer: identifies the item by its template + order and stores the value.
/// Validated against Platform form templates on draft save / submit.
/// </summary>
public sealed class OnboardingAnswer
{
    public Guid FormTemplateId { get; private set; }
    public int QuestionOrder { get; private set; }
    public string Value { get; private set; }

    public OnboardingAnswer(Guid formTemplateId, int questionOrder, string value)
    {
        FormTemplateId = formTemplateId;
        QuestionOrder = questionOrder;
        Value = value ?? string.Empty;
    }
}
