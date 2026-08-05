namespace FSH.Modules.Suppliers.Domain;

public sealed class VendorContact
{
    public string Name { get; private set; }
    public string Role { get; private set; }
    public string Email { get; private set; }
    public string Phone { get; private set; }
    public bool IsPrimary { get; private set; }

    public VendorContact(string name, string? role, string? email, string? phone, bool isPrimary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Role = (role ?? string.Empty).Trim();
        Email = (email ?? string.Empty).Trim();
        Phone = (phone ?? string.Empty).Trim();
        IsPrimary = isPrimary;
    }
}
