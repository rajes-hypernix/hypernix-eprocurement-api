namespace FSH.Modules.Suppliers.Domain;

/// <summary>Parses API/UI vendor-type strings into <see cref="VendorType"/>.</summary>
public static class VendorTypeParser
{
    public static VendorType Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return VendorType.NonSwec;
        var v = value.Trim().Replace("-", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        if (v.Equals("Swec", StringComparison.OrdinalIgnoreCase)) return VendorType.Swec;
        if (v.Equals("NonSwec", StringComparison.OrdinalIgnoreCase)) return VendorType.NonSwec;
        throw new ArgumentException($"Unknown vendor type '{value}'. Expected Swec or NonSwec.");
    }

    public static string ToApi(VendorType type) => type switch
    {
        VendorType.Swec => "Swec",
        _ => "NonSwec",
    };
}

public static class VendorAddressTypeParser
{
    public static VendorAddressType Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return VendorAddressType.Registered;
        if (Enum.TryParse<VendorAddressType>(value.Trim(), ignoreCase: true, out var parsed)) return parsed;
        throw new ArgumentException($"Unknown address type '{value}'.");
    }
}

public static class CertificationStatusParser
{
    public static CertificationStatus Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return CertificationStatus.Valid;
        if (Enum.TryParse<CertificationStatus>(value.Trim(), ignoreCase: true, out var parsed)) return parsed;
        throw new ArgumentException($"Unknown certification status '{value}'.");
    }
}
