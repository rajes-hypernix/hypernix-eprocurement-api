using FSH.Framework.Core.Domain;

namespace FSH.Modules.Suppliers.Domain;

public sealed class Vendor : AggregateRoot<Guid>
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string RegisteredName { get; private set; } = default!;
    public string Type { get; private set; } = "NonSwec";
    public string Status { get; private set; } = "Pending";
    public string Country { get; private set; } = "MY";
    public decimal Rating { get; private set; }
    public decimal CreditLimit { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    private Vendor() { }

    public static Vendor Create(string code, string name, string registeredName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(registeredName);

        return new Vendor
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim(),
            Name = name.Trim(),
            RegisteredName = registeredName.Trim(),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
        };
    }

    public void UpdateFinancials(decimal rating, decimal creditLimit)
    {
        Rating = rating;
        CreditLimit = creditLimit;
        UpdatedUtc = DateTime.UtcNow;
    }
}
