using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class State : AggregateRoot<Guid>
{
    public Guid CountryId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    private State() { }

    public static State Create(Guid countryId, string code, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (countryId == Guid.Empty)
            throw new ArgumentException("CountryId is required.", nameof(countryId));

        var now = DateTime.UtcNow;
        return new State
        {
            Id = Guid.CreateVersion7(),
            CountryId = countryId,
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
