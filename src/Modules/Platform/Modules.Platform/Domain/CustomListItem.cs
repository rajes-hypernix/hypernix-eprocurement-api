using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class CustomListItem : AggregateRoot<Guid>
{
    public Guid ListId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Label { get; private set; } = default!;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    private CustomListItem() { }

    public static CustomListItem Create(Guid listId, string code, string label, int sortOrder, bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (listId == Guid.Empty)
            throw new ArgumentException("ListId is required.", nameof(listId));

        var now = DateTime.UtcNow;
        return new CustomListItem
        {
            Id = Guid.CreateVersion7(),
            ListId = listId,
            Code = code.Trim().ToUpperInvariant(),
            Label = label.Trim(),
            SortOrder = sortOrder,
            IsActive = isActive,
            CreatedUtc = now,
            UpdatedUtc = now,
        };
    }

    internal void Update(string label, int sortOrder, bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        Label = label.Trim();
        SortOrder = sortOrder;
        IsActive = isActive;
        UpdatedUtc = DateTime.UtcNow;
    }
}
