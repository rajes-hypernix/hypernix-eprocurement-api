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
    public VendorStatus Status { get; private set; } = VendorStatus.Pending;
    public string Region { get; set; } = "Peninsular";
    public string State { get; set; } = "—";
    public string City { get; set; } = "—";
    public string Country { get; set; } = "MY";                  // ISO-2 code (Slice H T6)
    public decimal Rating { get; set; }
    public string PaymentTerms { get; set; } = "30 days nett";
    public decimal CreditLimit { get; set; }

    /// <summary>SWEC category leaf/branch codes the vendor is registered for.</summary>
    public List<string> Categories { get; set; } = [];

    // VendorPerformance is no longer stored — it is DERIVED (Slice H T7, VendorPerformanceView).
    public List<VendorContact> Contacts { get; set; } = [];
    public List<VendorAddress> Addresses { get; set; } = [];
    public List<VendorBankAccount> BankAccounts { get; set; } = [];
    public List<VendorCertification> Certifications { get; set; } = [];
    public List<VendorCurrency> Currencies { get; set; } = [];

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public Vendor() { }

    // ===== Status transitions (T3). =====

    /// <summary>Registers the vendor into the master immediately (manual entry, C1 — no approval).</summary>
    public void Register() => Status = VendorStatus.Registered;

    /// <summary>Provisional registration on onboarding promotion (Non-SWEC path).</summary>
    public void MarkProvisional() => Status = VendorStatus.Provisional;

    /// <summary>Toggles a vendor between active (Registered) and Inactive — the buyer's enable/disable.</summary>
    public void ToggleActive() =>
        Status = Status == VendorStatus.Inactive ? VendorStatus.Registered : VendorStatus.Inactive;

    /// <summary>TEST/SEED ONLY — sets the status directly, bypassing transitions. Never call from
    /// production service code (enforced by the ArchitectureTests source-scan).</summary>
    public Vendor SeededAs(VendorStatus status) { Status = status; return this; }
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
    public string Country { get; set; } = "MY";                  // ISO-2 code (Slice H T6)
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
