namespace eProcure.Domain.Suppliers;

public enum VendorType { Swec, NonSwec }

/// <summary>Lifecycle status; mirrors the prototype's vendor master badges.</summary>
public enum VendorStatus { Registered, Provisional, Pending, Inactive, Blacklisted }

/// <summary>
/// Supplier master record. In production these originate from NetSuite; for now
/// they are dummy/local seed data (CLAUDE.md integration scope). Child collections
/// (contacts/addresses/banking/certs/currencies) are owned by the vendor.
/// </summary>
public class Vendor
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; set; } = default!;        // e.g. SWK-V-10293
    public string Name { get; set; } = default!;
    public string RegisteredName { get; set; } = default!;
    public string RegistrationNo { get; set; } = "—";   // SSM / CCM
    public string TaxId { get; set; } = "—";
    public VendorType Type { get; set; } = VendorType.NonSwec;
    public string? LlrcTier { get; set; }
    public VendorStatus Status { get; set; } = VendorStatus.Pending;
    public string Region { get; set; } = "Peninsular";
    public string State { get; set; } = "—";
    public string City { get; set; } = "—";
    public string Country { get; set; } = "Malaysia";
    public decimal Rating { get; set; }
    public string PaymentTerms { get; set; } = "30 days nett";
    public decimal CreditLimit { get; set; }

    /// <summary>SWEC category leaf/branch codes the vendor is registered for.</summary>
    public List<string> Categories { get; set; } = [];

    public VendorPerformance Performance { get; set; } = new();
    public List<VendorContact> Contacts { get; set; } = [];
    public List<VendorAddress> Addresses { get; set; } = [];
    public List<VendorBankAccount> BankAccounts { get; set; } = [];
    public List<VendorCertification> Certifications { get; set; } = [];
    public List<VendorCurrency> Currencies { get; set; } = [];

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public Vendor() { }
}

public class VendorPerformance
{
    public int Otd { get; set; }            // on-time delivery %
    public int Quality { get; set; }        // quality acceptance %
    public int Breaches { get; set; }
    public int Lead { get; set; }           // avg lead time (weeks)
    public int Response { get; set; }       // RFQ response rate %
    public int WinRate { get; set; }
    public decimal SpendYtd { get; set; }
    public int Pos { get; set; }
}

public class VendorContact
{
    public string Name { get; set; } = default!;
    public string Role { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public bool IsPrimary { get; set; }
}

public class VendorAddress
{
    public string Type { get; set; } = "Registered";
    public string Line { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Country { get; set; } = "Malaysia";
    public string Postcode { get; set; } = "";
    public bool IsPrimary { get; set; }
}

public class VendorBankAccount
{
    public string Bank { get; set; } = default!;
    public string AccountNo { get; set; } = "";
    public string Swift { get; set; } = "";
    public string Currency { get; set; } = "MYR";
    public bool IsPrimary { get; set; }
}

public class VendorCertification
{
    public string Name { get; set; } = default!;
    public string Number { get; set; } = "";
    public string ValidTo { get; set; } = "";
    public string Status { get; set; } = "Valid";
}

public class VendorCurrency
{
    public string Code { get; set; } = "MYR";
    public bool IsPrimary { get; set; }
}
