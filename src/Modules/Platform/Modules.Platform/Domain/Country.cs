using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class Country : AggregateRoot<Guid>
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    private Country() { }

    public static Country Create(string code, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var now = DateTime.UtcNow;
        return new Country
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim().ToUpperInvariant(),
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
}
