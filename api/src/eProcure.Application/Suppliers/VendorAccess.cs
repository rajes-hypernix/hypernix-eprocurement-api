using eProcure.Application.Abstractions;

namespace eProcure.Application.Suppliers;

/// <summary>
/// Access-scoping groundwork (BUSINESS-RULES [G]). A vendor principal may only
/// touch records belonging to its own vendor; internal users are unrestricted here.
/// Used by vendor-scoped endpoints from the bidding slice onward.
/// </summary>
public static class VendorAccess
{
    /// <summary>True if the principal is a vendor user bound to a different vendor.</summary>
    public static bool IsForeignVendor(ICurrentUser user, Guid vendorId) =>
        user.VendorId is { } own && own != vendorId;

    /// <summary>Throws <see cref="ForbiddenException"/> if the principal may not access the vendor.</summary>
    public static void EnsureCanAccess(ICurrentUser user, Guid vendorId)
    {
        if (IsForeignVendor(user, vendorId))
            throw new ForbiddenException("Vendor users may only access their own vendor's records.");
    }
}
