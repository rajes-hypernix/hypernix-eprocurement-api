using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class CustomList : AggregateRoot<Guid>
{
    private readonly List<CustomListItem> _items = [];

    public string Key { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    public IReadOnlyList<CustomListItem> Items => _items;

    private CustomList() { }

    public static CustomList Create(string key, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var now = DateTime.UtcNow;
        return new CustomList
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

    public CustomListItem UpsertItem(string code, string label, int sortOrder, bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var normalized = code.Trim().ToUpperInvariant();
        var existing = _items.FirstOrDefault(i =>
            string.Equals(i.Code, normalized, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.Update(label, sortOrder, isActive);
            UpdatedUtc = DateTime.UtcNow;
            return existing;
        }

        var item = CustomListItem.Create(Id, normalized, label, sortOrder, isActive);
        _items.Add(item);
        UpdatedUtc = DateTime.UtcNow;
        return item;
    }
}
