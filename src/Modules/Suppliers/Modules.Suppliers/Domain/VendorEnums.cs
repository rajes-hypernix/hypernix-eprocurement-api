namespace FSH.Modules.Suppliers.Domain;

/// <summary>SWEC vs non-SWEC vendor classification (invitation, application, and master).</summary>
public enum VendorType
{
    NonSwec = 0,
    Swec = 1,
}

/// <summary>Vendor master lifecycle.</summary>
public enum VendorStatus
{
    Pending = 0,
    Registered = 1,
    Provisional = 2,
    Inactive = 3,
}

/// <summary>Owned address purpose on vendor / onboarding staging.</summary>
public enum VendorAddressType
{
    Registered = 0,
    Business = 1,
    Delivery = 2,
    Billing = 3,
}

/// <summary>Certification validity flag on vendor / onboarding staging.</summary>
public enum CertificationStatus
{
    Valid = 0,
    Expired = 1,
    Pending = 2,
}
