using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class FormTemplateQuestion : AggregateRoot<Guid>
{
    public Guid TemplateId { get; private set; }
    public int Order { get; private set; }
    public string Label { get; private set; } = default!;
    public string Type { get; private set; } = default!;
    public bool Required { get; private set; }
    public string? ConfigJson { get; private set; }
    public string? Help { get; private set; }

    private FormTemplateQuestion() { }

    public static FormTemplateQuestion Create(
        Guid templateId,
        int order,
        string label,
        string type,
        bool required,
        string? configJson,
        string? help)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        if (templateId == Guid.Empty)
            throw new ArgumentException("TemplateId is required.", nameof(templateId));
        ArgumentOutOfRangeException.ThrowIfNegative(order);
        return new FormTemplateQuestion
        {
            Id = Guid.CreateVersion7(),
            TemplateId = templateId,
            Order = order,
            Label = label.Trim(),
            Type = type.Trim().ToLowerInvariant(),
            Required = required,
            ConfigJson = string.IsNullOrWhiteSpace(configJson) ? null : configJson,
            Help = string.IsNullOrWhiteSpace(help) ? null : help.Trim(),
        };
    }
}
