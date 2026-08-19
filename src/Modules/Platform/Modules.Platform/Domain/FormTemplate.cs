using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class FormTemplate : AggregateRoot<Guid>, IAuditableEntity
{
    private readonly List<FormTemplateQuestion> _questions = [];

    public string Key { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    public IReadOnlyList<FormTemplateQuestion> Questions => _questions;

    private FormTemplate() { }

    public static FormTemplate Create(string key, string name, string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new FormTemplate
        {
            Id = Guid.CreateVersion7(),
            Key = key.Trim(),
            Name = name.Trim(),
            CreatedOnUtc = TimeProvider.System.GetUtcNow(),
            CreatedBy = createdBy,
        };
    }

    public void SetActive(bool isActive, string? modifiedBy = null)
    {
        IsActive = isActive;
        Touch(modifiedBy);
    }

    public void Rename(string name, string? modifiedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Touch(modifiedBy);
    }

    public FormTemplateQuestion AddQuestion(
        int order,
        string label,
        string type,
        bool required,
        string? configJson = null,
        string? help = null,
        string? modifiedBy = null)
    {
        if (_questions.Any(q => q.Order == order))
            throw new PlatformRuleException($"Question order {order} already exists on this template.");

        var question = FormTemplateQuestion.Create(Id, order, label, type, required, configJson, help, modifiedBy);
        _questions.Add(question);
        Touch(modifiedBy);
        return question;
    }

    public void ReplaceQuestions(
        IEnumerable<(int Order, string Label, string Type, bool Required, string? ConfigJson, string? Help)> questions,
        string? modifiedBy = null)
    {
        _questions.Clear();
        foreach (var q in questions.OrderBy(x => x.Order))
            AddQuestion(q.Order, q.Label, q.Type, q.Required, q.ConfigJson, q.Help, modifiedBy);
    }

    private void Touch(string? modifiedBy)
    {
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }
}
