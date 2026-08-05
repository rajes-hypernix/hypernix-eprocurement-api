namespace FSH.Modules.Suppliers.Domain;

public sealed class VendorAddress
{
    public VendorAddressType Type { get; private set; }
    public string Line { get; private set; }
    /// <summary>Denormalized display name (filled from Platform City when <see cref="CityId"/> is set).</summary>
    public string City { get; private set; }
    /// <summary>Denormalized display name (filled from Platform State when <see cref="StateId"/> is set).</summary>
    public string State { get; private set; }
    /// <summary>ISO 3166-1 alpha-2 — logical FK to Platform <c>Countries.Code</c>.</summary>
    public string CountryCode { get; private set; }
    public Guid? StateId { get; private set; }
    public Guid? CityId { get; private set; }
    public string Postcode { get; private set; }
    public bool IsPrimary { get; private set; }

    public VendorAddress(
        VendorAddressType type,
        string? line,
        string? city,
        string? state,
        string? countryCode,
        Guid? stateId,
        Guid? cityId,
        string? postcode,
        bool isPrimary)
    {
        Type = type;
        Line = (line ?? string.Empty).Trim();
        City = (city ?? string.Empty).Trim();
        State = (state ?? string.Empty).Trim();
        CountryCode = NormalizeCountryCode(countryCode);
        StateId = stateId;
        CityId = cityId;
        Postcode = (postcode ?? string.Empty).Trim();
        IsPrimary = isPrimary;
    }

    private static string NormalizeCountryCode(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) return "MY";
        var code = countryCode.Trim().ToUpperInvariant();
        if (code.Length == 2 && code.All(char.IsAsciiLetter)) return code;
        // Tolerate legacy full name from onboarding drafts.
        if (code.Equals("MALAYSIA", StringComparison.OrdinalIgnoreCase)) return "MY";
        throw new ArgumentException("Country code must be ISO 3166-1 alpha-2 (e.g. MY).");
    }
}
