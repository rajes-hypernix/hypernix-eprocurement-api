using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class CustomList : AggregateRoot<Guid>, IAuditableEntity
{
    private readonly List<CustomListItem> _items = [];

    public string Key { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    public IReadOnlyList<CustomListItem> Items => _items;

    private CustomList() { }

    public static CustomList Create(string key, string name, string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new CustomList
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

    public void Update(string name, string? modifiedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Touch(modifiedBy);
    }

    public CustomListItem UpsertItem(string code, string label, int sortOrder, bool isActive, string? modifiedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var normalized = code.Trim().ToUpperInvariant();
        var existing = _items.FirstOrDefault(i =>
            string.Equals(i.Code, normalized, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.Update(label, sortOrder, isActive, modifiedBy);
            Touch(modifiedBy);
            return existing;
        }

        var item = CustomListItem.Create(Id, normalized, label, sortOrder, isActive, modifiedBy);
        _items.Add(item);
        Touch(modifiedBy);
        return item;
    }

    private void Touch(string? modifiedBy)
    {
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }
}
