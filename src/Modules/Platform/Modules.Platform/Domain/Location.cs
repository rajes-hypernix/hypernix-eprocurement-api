using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public sealed class Location : AggregateRoot<Guid>, IAuditableEntity
{
    private readonly List<LocationAddress> _addresses = [];

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public IReadOnlyCollection<LocationAddress> Addresses => _addresses;

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    private Location() { }

    public static Location Create(
        string code,
        string name,
        IEnumerable<LocationAddressDraft> addresses,
        bool isActive = true,
        string? createdBy = null)
    {
        var loc = new Location
        {
            Id = Guid.CreateVersion7(),
            CreatedOnUtc = TimeProvider.System.GetUtcNow(),
            CreatedBy = createdBy,
        };
        loc.Apply(code, name, addresses, isActive, isCreate: true);
        return loc;
    }

    public void Update(string name, IEnumerable<LocationAddressDraft> addresses, string? modifiedBy = null)
    {
        Apply(Code, name, addresses, IsActive, isCreate: false);
        Touch(modifiedBy);
    }

    public void SetActive(bool isActive, string? modifiedBy = null)
    {
        IsActive = isActive;
        Touch(modifiedBy);
    }

    private void Apply(string code, string name, IEnumerable<LocationAddressDraft> addresses, bool isActive, bool isCreate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (isCreate)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code);
            var normalized = code.Trim().ToUpperInvariant();
            if (normalized.Length > 40)
                throw new PlatformRuleException("Location code must be at most 40 characters.");
            Code = normalized;
        }

        Name = name.Trim();
        if (Name.Length > 80)
            throw new PlatformRuleException("Location name must be at most 80 characters.");
        IsActive = isActive;

        var drafts = addresses.ToList();
        if (drafts.Count == 0)
            throw new PlatformRuleException("Location requires at least one address.");

        _addresses.Clear();
        var defaultAssigned = false;
        foreach (var (draft, index) in drafts.Select((d, i) => (d, i)))
        {
            var isDefault = draft.IsDefault && !defaultAssigned;
            if (isDefault) defaultAssigned = true;
            _addresses.Add(LocationAddress.Create(
                draft with { IsDefault = isDefault },
                index));
        }

        if (!defaultAssigned)
            _addresses[0].MarkDefault(true);
    }

    private void Touch(string? modifiedBy)
    {
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }
}

public sealed class LocationAddress
{
    public Guid Id { get; private set; }
    public string Label { get; private set; } = default!;
    public string Line1 { get; private set; } = default!;
    public string? Line2 { get; private set; }
    public string City { get; private set; } = default!;
    public string State { get; private set; } = default!;
    public string Postcode { get; private set; } = default!;
    public string Country { get; private set; } = "MY";
    public bool IsDefault { get; private set; }
    public int Sort { get; private set; }

    private LocationAddress() { }

    internal static LocationAddress Create(LocationAddressDraft draft, int fallbackSort)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.Label);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.Line1);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.City);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.State);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.Postcode);

        var country = string.IsNullOrWhiteSpace(draft.Country) ? "MY" : draft.Country.Trim().ToUpperInvariant();
        if (country.Length != 2)
            throw new PlatformRuleException("Address country must be a 2-letter ISO code.");

        return new LocationAddress
        {
            Id = Guid.CreateVersion7(),
            Label = draft.Label.Trim(),
            Line1 = draft.Line1.Trim(),
            Line2 = string.IsNullOrWhiteSpace(draft.Line2) ? null : draft.Line2.Trim(),
            City = draft.City.Trim(),
            State = draft.State.Trim(),
            Postcode = draft.Postcode.Trim(),
            Country = country,
            Sort = draft.Sort ?? fallbackSort,
            IsDefault = draft.IsDefault,
        };
    }

    internal void MarkDefault(bool isDefault) => IsDefault = isDefault;
}

public sealed record LocationAddressDraft(
    string Label,
    string Line1,
    string City,
    string State,
    string Postcode,
    string? Line2 = null,
    string? Country = "MY",
    bool IsDefault = false,
    int? Sort = null);
