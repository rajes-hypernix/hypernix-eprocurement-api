using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class Item : AggregateRoot<Guid>, IAuditableEntity
{
    public string ItemCode { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string Uom { get; private set; } = "Unit";
    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    private Item() { }

    public static Item Create(string itemCode, string description, string? uom = null, bool isActive = true, string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return new Item
        {
            Id = Guid.CreateVersion7(),
            ItemCode = itemCode.Trim().ToUpperInvariant(),
            Description = description.Trim(),
            Uom = string.IsNullOrWhiteSpace(uom) ? "Unit" : uom.Trim(),
            IsActive = isActive,
            CreatedOnUtc = TimeProvider.System.GetUtcNow(),
            CreatedBy = createdBy,
        };
    }

    public void Update(string itemCode, string description, string uom, string? modifiedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(uom);
        ItemCode = itemCode.Trim().ToUpperInvariant();
        Description = description.Trim();
        Uom = uom.Trim();
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }

    public void SetActive(bool isActive, string? modifiedBy = null)
    {
        IsActive = isActive;
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }
}
