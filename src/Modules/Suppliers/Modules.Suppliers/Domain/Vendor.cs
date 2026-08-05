using FSH.Framework.Core.Domain;

namespace FSH.Modules.Suppliers.Domain;

public sealed class Vendor : AggregateRoot<Guid>, IAuditableEntity
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
    public VendorType Type { get; private set; } = VendorType.NonSwec;
    public string? LlrcTier { get; private set; }
    public VendorStatus Status { get; private set; } = VendorStatus.Pending;
    public string Region { get; private set; } = "Peninsular";
    /// <summary>Denormalized state name for lists/display.</summary>
    public string State { get; private set; } = "—";
    /// <summary>Denormalized city name for lists/display.</summary>
    public string City { get; private set; } = "—";
    /// <summary>ISO country code — logical FK to Platform Countries.Code.</summary>
    public string CountryCode { get; private set; } = "MY";
    public Guid? StateId { get; private set; }
    public Guid? CityId { get; private set; }
    public decimal Rating { get; private set; }
    public string PaymentTerms { get; private set; } = "30 days nett";
    public decimal CreditLimit { get; private set; }
    public List<string> Categories { get; private set; } = [];
    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

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

        var vendor = new Vendor
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim(),
            Name = name.Trim(),
            RegisteredName = registeredName.Trim(),
            CreatedOnUtc = AuditTime.UtcNow,
            CreatedBy = null,
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
        VendorType type,
        string? region,
        string? state,
        string? city,
        string? countryCode,
        Guid? stateId,
        Guid? cityId,
        string? paymentTerms,
        IReadOnlyList<string>? categories)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var vendor = new Vendor
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim(),
            Name = name.Trim(),
            RegisteredName = string.IsNullOrWhiteSpace(registeredName) ? name.Trim() : registeredName.Trim(),
            RegistrationNo = string.IsNullOrWhiteSpace(registrationNo) ? "—" : registrationNo.Trim(),
            TaxId = string.IsNullOrWhiteSpace(taxId) ? "—" : taxId.Trim(),
            Type = type,
            Region = string.IsNullOrWhiteSpace(region) ? "Peninsular" : region.Trim(),
            State = string.IsNullOrWhiteSpace(state) ? "—" : state.Trim(),
            City = string.IsNullOrWhiteSpace(city) ? "—" : city.Trim(),
            CountryCode = NormalizeCountryCode(countryCode),
            StateId = stateId,
            CityId = cityId,
            PaymentTerms = string.IsNullOrWhiteSpace(paymentTerms) ? "NET30" : paymentTerms.Trim(),
            Categories = categories is { Count: > 0 } ? [.. categories.Distinct()] : [],
            CreatedOnUtc = AuditTime.UtcNow,
            CreatedBy = null,
        };
        vendor.Register();
        return vendor;
    }

    public void UpdateProfile(
        string name,
        string registeredName,
        string registrationNo,
        string taxId,
        VendorType type,
        string? llrcTier,
        string region,
        string state,
        string city,
        string countryCode,
        Guid? stateId,
        Guid? cityId,
        string paymentTerms,
        decimal creditLimit,
        decimal rating)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(registeredName);
        ArgumentException.ThrowIfNullOrWhiteSpace(registrationNo);
        ArgumentException.ThrowIfNullOrWhiteSpace(taxId);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentTerms);

        Name = name.Trim();
        RegisteredName = registeredName.Trim();
        RegistrationNo = registrationNo.Trim();
        TaxId = taxId.Trim();
        Type = type;
        LlrcTier = llrcTier;
        Region = region.Trim();
        State = state.Trim();
        City = city.Trim();
        CountryCode = NormalizeCountryCode(countryCode);
        StateId = stateId;
        CityId = cityId;
        PaymentTerms = paymentTerms.Trim();
        CreditLimit = creditLimit;
        Rating = rating;
        LastModifiedOnUtc = AuditTime.UtcNow;
        LastModifiedBy = null;
    }

    public void SetEntryForm(Guid? entryFormId)
    {
        EntryFormId = entryFormId;
        LastModifiedOnUtc = AuditTime.UtcNow;
        LastModifiedBy = null;
    }

    public void SetCategories(IReadOnlyList<string> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);
        Categories = [.. categories.Distinct()];
        LastModifiedOnUtc = AuditTime.UtcNow;
        LastModifiedBy = null;
    }

    public void AddContact(VendorContact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);
        EnsureContactUnique(contact);
        _contacts.Add(contact);
        LastModifiedOnUtc = AuditTime.UtcNow;
        LastModifiedBy = null;
    }

    public void ReplaceContacts(IEnumerable<VendorContact> contacts)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        _contacts.Clear();
        foreach (var c in contacts)
        {
            EnsureContactUnique(c);
            _contacts.Add(c);
        }

        LastModifiedOnUtc = AuditTime.UtcNow;
        LastModifiedBy = null;
    }

    public void AddAddress(VendorAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        _addresses.Add(address);
        LastModifiedOnUtc = AuditTime.UtcNow;
        LastModifiedBy = null;
    }

    public void AddBankAccount(VendorBankAccount bankAccount)
    {
        ArgumentNullException.ThrowIfNull(bankAccount);
        _bankAccounts.Add(bankAccount);
        LastModifiedOnUtc = AuditTime.UtcNow;
        LastModifiedBy = null;
    }

    public void AddCertification(VendorCertification certification)
    {
        ArgumentNullException.ThrowIfNull(certification);
        _certifications.Add(certification);
        LastModifiedOnUtc = AuditTime.UtcNow;
        LastModifiedBy = null;
    }

    public void AddCurrency(VendorCurrency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        _currencies.Add(currency);
        LastModifiedOnUtc = AuditTime.UtcNow;
        LastModifiedBy = null;
    }

    /// <summary>Manual entry / legacy path — no approval workflow.</summary>
    public void Register()
    {
        Status = VendorStatus.Registered;
        LastModifiedOnUtc = AuditTime.UtcNow;
        LastModifiedBy = null;
    }

    /// <summary>Non-SWEC onboarding promotion — provisional until fully qualified.</summary>
    public void MarkProvisional()
    {
        Status = VendorStatus.Provisional;
        LastModifiedOnUtc = AuditTime.UtcNow;
        LastModifiedBy = null;
    }

    /// <summary>Buyer/Admin master-data governance toggle between Registered and Inactive.</summary>
    public void ToggleActive()
    {
        Status = Status == VendorStatus.Inactive ? VendorStatus.Registered : VendorStatus.Inactive;
        LastModifiedOnUtc = AuditTime.UtcNow;
        LastModifiedBy = null;
    }

    private void EnsureContactUnique(VendorContact contact)
    {
        var email = contact.Email;
        if (!string.IsNullOrWhiteSpace(email)
            && _contacts.Any(c => string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A contact with email '{email}' already exists on this vendor.");
        }

        if (_contacts.Any(c =>
                string.Equals(c.Name, contact.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(c.Phone ?? "", contact.Phone ?? "", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(email)))
        {
            throw new InvalidOperationException($"A contact named '{contact.Name}' already exists on this vendor.");
        }
    }

    private static string NormalizeCountryCode(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) return "MY";
        var code = countryCode.Trim().ToUpperInvariant();
        if (code.Length == 2 && code.All(char.IsAsciiLetter)) return code;
        if (code.Equals("MALAYSIA", StringComparison.OrdinalIgnoreCase)) return "MY";
        throw new ArgumentException("Country code must be ISO 3166-1 alpha-2 (e.g. MY).");
    }
}
