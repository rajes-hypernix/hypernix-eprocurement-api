using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class FormTemplate : AggregateRoot<Guid>
{
    private readonly List<FormTemplateQuestion> _questions = [];

    public string Key { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    public IReadOnlyList<FormTemplateQuestion> Questions => _questions;

    private FormTemplate() { }

    public static FormTemplate Create(string key, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var now = DateTime.UtcNow;
        return new FormTemplate
        {
            Id = Guid.CreateVersion7(),
            Key = key.Trim(),
            Name = name.Trim(),
            CreatedUtc = now,
            UpdatedUtc = now,
        };
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedUtc = DateTime.UtcNow;
    }

    public FormTemplateQuestion AddQuestion(
        int order,
        string label,
        string type,
        bool required,
        string? configJson = null,
        string? help = null)
    {
        if (_questions.Any(q => q.Order == order))
            throw new PlatformRuleException($"Question order {order} already exists on this template.");

        var question = FormTemplateQuestion.Create(Id, order, label, type, required, configJson, help);
        _questions.Add(question);
        UpdatedUtc = DateTime.UtcNow;
        return question;
    }

    public void ReplaceQuestions(IEnumerable<(int Order, string Label, string Type, bool Required, string? ConfigJson, string? Help)> questions)
    {
        _questions.Clear();
        foreach (var q in questions.OrderBy(x => x.Order))
            AddQuestion(q.Order, q.Label, q.Type, q.Required, q.ConfigJson, q.Help);
    }
}
