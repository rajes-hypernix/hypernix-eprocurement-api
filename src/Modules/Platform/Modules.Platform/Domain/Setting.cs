using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

/// <summary>Platform setting store — seeded keys only (timezone, base currency, …).</summary>
public sealed class Setting : AggregateRoot<Guid>, IAuditableEntity
{
    public string Key { get; private set; } = default!;
    public string Value { get; private set; } = default!;
    /// <summary>Discriminator for UI editing: TimeZone | Currency | Text.</summary>
    public string ValueKind { get; private set; } = default!;
    public string Label { get; private set; } = default!;
    public string? Description { get; private set; }

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    private Setting() { }

    public static Setting Create(
        string key,
        string value,
        string valueKind,
        string label,
        string? description = null,
        Guid? id = null,
        string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return new Setting
        {
            Id = id ?? Guid.CreateVersion7(),
            Key = key.Trim(),
            Value = value.Trim(),
            ValueKind = valueKind.Trim(),
            Label = label.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatedOnUtc = TimeProvider.System.GetUtcNow(),
            CreatedBy = createdBy,
        };
    }

    public void SetValue(string value, string? modifiedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }
}

public static class SettingKeys
{
    public const string TimeZone = "Platform:TimeZone";
    public const string BaseCurrency = "Platform:BaseCurrency";
}
