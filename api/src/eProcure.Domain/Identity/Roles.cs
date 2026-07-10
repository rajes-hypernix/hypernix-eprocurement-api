namespace eProcure.Domain.Identity;

/// <summary>
/// Canonical role names (also the JWT role claim values). Internal users may hold
/// any of the internal roles; <see cref="Vendor"/> is reserved for VendorUser
/// principals and must never be granted to an internal User (SoD, BUSINESS-RULES [G]).
/// </summary>
public static class Roles
{
    public const string Buyer = "Buyer";
    public const string Approver = "Approver";
    public const string TechEvaluator = "TechEvaluator";
    public const string CommEvaluator = "CommEvaluator";
    public const string Admin = "Admin";
    public const string Vendor = "Vendor";

    public static readonly IReadOnlyList<string> Internal =
        [Buyer, Approver, TechEvaluator, CommEvaluator, Admin];

    public static readonly IReadOnlyList<string> All =
        [Buyer, Approver, TechEvaluator, CommEvaluator, Admin, Vendor];

    public static bool IsInternal(string role) => Internal.Contains(role);
}
