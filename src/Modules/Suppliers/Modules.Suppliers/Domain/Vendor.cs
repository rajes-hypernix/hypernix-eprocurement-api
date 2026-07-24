using FSH.Framework.Core.Domain;

namespace FSH.Modules.Suppliers.Domain;

public sealed class Vendor : AggregateRoot<Guid>
{
    private readonly List<VendorContact> _contacts = [];
    private readonly List<VendorAddress> _addresses = [];
    private readonly List<VendorBankAccount> _bankAccounts = [];
    private readonly List<VendorCertification> _certifications = [];
    private readonly List<VendorCurrency> _currencies = [];

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string RegisteredName { get; private set; } = default!;
    public string RegistrationNo { get; private set; } = "—";
    public string TaxId { get; private set; } = "—";
    public string Type { get; private set; } = "NonSwec";
    public string? LlrcTier { get; private set; }
    public string Status { get; private set; } = "Pending";
    public string Region { get; private set; } = "Peninsular";
    public string State { get; private set; } = "—";
    public string City { get; private set; } = "—";
    public string Country { get; private set; } = "MY";
    public decimal Rating { get; private set; }
    public string PaymentTerms { get; private set; } = "30 days nett";
    public decimal CreditLimit { get; private set; }
    public List<string> Categories { get; private set; } = [];
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    /// <summary>Opaque entry-form layout choice (Phase 5/6) — mirrors the same stub on <c>PurchaseRequisition</c>.</summary>
    public Guid? EntryFormId { get; private set; }

    public IReadOnlyList<VendorContact> Contacts => _contacts;
    public IReadOnlyList<VendorAddress> Addresses => _addresses;
    public IReadOnlyList<VendorBankAccount> BankAccounts => _bankAccounts;
    public IReadOnlyList<VendorCertification> Certifications => _certifications;
    public IReadOnlyList<VendorCurrency> Currencies => _currencies;

    private Vendor() { }

    public static Vendor Create(string code, string name, string registeredName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(registeredName);

        var now = DateTime.UtcNow;
        var vendor = new Vendor
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim(),
            Name = name.Trim(),
            RegisteredName = registeredName.Trim(),
            CreatedUtc = now,
            UpdatedUtc = now,
        };
        vendor._currencies.Add(new VendorCurrency("MYR", true));
        return vendor;
    }

    /// <summary>
    /// Manual New-Vendor entry: goes straight to Registered, no approval workflow (mirrors old CreateManualAsync).
    /// </summary>
    public static Vendor CreateManual(
        string code,
        string name,
        string? registeredName,
        string? registrationNo,
        string? taxId,
        string type,
        string? region,
        string? state,
        string? city,
        string? country,
        string? paymentTerms,
        IReadOnlyList<string>? categories)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var now = DateTime.UtcNow;
        var vendor = new Vendor
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim(),
            Name = name.Trim(),
            RegisteredName = string.IsNullOrWhiteSpace(registeredName) ? name.Trim() : registeredName.Trim(),
            RegistrationNo = string.IsNullOrWhiteSpace(registrationNo) ? "—" : registrationNo.Trim(),
            TaxId = string.IsNullOrWhiteSpace(taxId) ? "—" : taxId.Trim(),
            Type = type,
            Region = string.IsNullOrWhiteSpace(region) ? "Peninsular" : region,
            State = string.IsNullOrWhiteSpace(state) ? "—" : state,
            City = string.IsNullOrWhiteSpace(city) ? "—" : city,
            Country = string.IsNullOrWhiteSpace(country) ? "MY" : country,
            PaymentTerms = string.IsNullOrWhiteSpace(paymentTerms) ? "NET30" : paymentTerms,
            Categories = categories is { Count: > 0 } ? [.. categories.Distinct()] : [],
            CreatedUtc = now,
            UpdatedUtc = now,
        };
        vendor.Register();
        return vendor;
    }

    public void UpdateProfile(
        string name,
        string registeredName,
        string registrationNo,
        string taxId,
        string type,
        string? llrcTier,
        string region,
        string state,
        string city,
        string paymentTerms,
        decimal creditLimit,
        decimal rating)
    {
        Name = name;
        RegisteredName = registeredName;
        RegistrationNo = registrationNo;
        TaxId = taxId;
        Type = type;
        LlrcTier = llrcTier;
        Region = region;
        State = state;
        City = city;
        PaymentTerms = paymentTerms;
        CreditLimit = creditLimit;
        Rating = rating;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void SetEntryForm(Guid? entryFormId)
    {
        EntryFormId = entryFormId;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void SetCategories(IReadOnlyList<string> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);
        Categories = [.. categories.Distinct()];
        UpdatedUtc = DateTime.UtcNow;
    }

    public void AddContact(VendorContact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);
        _contacts.Add(contact);
    }

    public void AddAddress(VendorAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        _addresses.Add(address);
    }

    public void AddBankAccount(VendorBankAccount bankAccount)
    {
        ArgumentNullException.ThrowIfNull(bankAccount);
        _bankAccounts.Add(bankAccount);
    }

    public void AddCertification(VendorCertification certification)
    {
        ArgumentNullException.ThrowIfNull(certification);
        _certifications.Add(certification);
    }

    public void AddCurrency(VendorCurrency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        _currencies.Add(currency);
    }

    /// <summary>Manual entry / legacy path — no approval workflow.</summary>
    public void Register()
    {
        Status = "Registered";
        UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>Non-SWEC onboarding promotion — provisional until fully qualified.</summary>
    public void MarkProvisional()
    {
        Status = "Provisional";
        UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>Buyer/Admin master-data governance toggle between Registered and Inactive.</summary>
    public void ToggleActive()
    {
        Status = Status == "Inactive" ? "Registered" : "Inactive";
        UpdatedUtc = DateTime.UtcNow;
    }
}
