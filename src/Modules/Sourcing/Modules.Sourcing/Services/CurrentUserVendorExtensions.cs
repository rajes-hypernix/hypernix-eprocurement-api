using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Constants;

namespace FSH.Modules.Sourcing.Services;

/// <summary>
/// Reads the vendor-portal <c>vendorId</c> claim (see <see cref="ClaimConstants.VendorId"/>,
/// stamped by Identity's <c>CreateBasicClaims</c> for vendor-provisioned logins). Sourcing-local
/// rather than a change to the shared <see cref="ICurrentUser"/> interface — only vendor-scoped
/// bid/clarification endpoints need it.
/// </summary>
internal static class CurrentUserVendorExtensions
{
    public static Guid? GetVendorId(this ICurrentUser currentUser)
    {
        var claim = currentUser?.GetUserClaims()?.FirstOrDefault(c => c.Type == ClaimConstants.VendorId);
        return Guid.TryParse(claim?.Value, out var vendorId) ? vendorId : null;
    }

    /// <summary>Throws 403 when the caller has no vendor-portal login at all (not an ownership check on a specific resource).</summary>
    public static Guid RequireVendorId(this ICurrentUser currentUser) =>
        currentUser.GetVendorId() ?? throw new ForbiddenException("A vendor-portal login is required.");
}
