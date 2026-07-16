namespace FSH.Modules.Suppliers.Domain;

public sealed class VendorAddress
{
    public string Type { get; private set; }
    public string Line { get; private set; }
    public string City { get; private set; }
    public string State { get; private set; }
    public string Country { get; private set; }
    public string Postcode { get; private set; }
    public bool IsPrimary { get; private set; }

    public VendorAddress(string? type, string? line, string? city, string? state, string? country, string? postcode, bool isPrimary)
    {
        Type = string.IsNullOrWhiteSpace(type) ? "Registered" : type;
        Line = line ?? string.Empty;
        City = city ?? string.Empty;
        State = state ?? string.Empty;
        Country = string.IsNullOrWhiteSpace(country) ? "MY" : country;
        Postcode = postcode ?? string.Empty;
        IsPrimary = isPrimary;
    }
}
