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
        Name = name;
        Role = role ?? string.Empty;
        Email = email ?? string.Empty;
        Phone = phone ?? string.Empty;
        IsPrimary = isPrimary;
    }
}
