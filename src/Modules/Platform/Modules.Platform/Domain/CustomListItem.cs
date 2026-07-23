using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class CustomListItem : AggregateRoot<Guid>, IAuditableEntity
{
    public Guid ListId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Label { get; private set; } = default!;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    private CustomListItem() { }

    public static CustomListItem Create(
        Guid listId,
        string code,
        string label,
        int sortOrder,
        bool isActive,
        string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (listId == Guid.Empty)
            throw new ArgumentException("ListId is required.", nameof(listId));

        return new CustomListItem
        {
            Id = Guid.CreateVersion7(),
            ListId = listId,
            Code = code.Trim().ToUpperInvariant(),
            Label = label.Trim(),
            SortOrder = sortOrder,
            IsActive = isActive,
            CreatedOnUtc = TimeProvider.System.GetUtcNow(),
            CreatedBy = createdBy,
        };
    }

    internal void Update(string label, int sortOrder, bool isActive, string? modifiedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        Label = label.Trim();
        SortOrder = sortOrder;
        IsActive = isActive;
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }
}
