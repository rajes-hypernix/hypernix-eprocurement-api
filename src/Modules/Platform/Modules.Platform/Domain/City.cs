using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class City : AggregateRoot<Guid>
{
    public Guid StateId { get; private set; }
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    private City() { }

    public static City Create(Guid stateId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (stateId == Guid.Empty)
            throw new ArgumentException("StateId is required.", nameof(stateId));

        var now = DateTime.UtcNow;
        return new City
        {
            Id = Guid.CreateVersion7(),
            StateId = stateId,
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
