using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Constants;

namespace FSH.Modules.Procurement.Services;

internal static class CurrentUserVendorExtensions
{
    public static Guid? GetVendorId(this ICurrentUser currentUser)
    {
        var claim = currentUser?.GetUserClaims()?.FirstOrDefault(c => c.Type == ClaimConstants.VendorId);
        return Guid.TryParse(claim?.Value, out var vendorId) ? vendorId : null;
    }

    public static Guid RequireVendorId(this ICurrentUser currentUser) =>
        currentUser.GetVendorId() ?? throw new ForbiddenException("A vendor-portal login is required.");

    /// <summary>Vendor logins may only read records owned by their own vendorId claim.</summary>
    public static void EnsureOwns(this ICurrentUser currentUser, Guid resourceVendorId)
    {
        if (currentUser.GetVendorId() is { } vid && vid != resourceVendorId)
        {
            throw new ForbiddenException("This record is not assigned to your vendor.");
        }
    }
}
