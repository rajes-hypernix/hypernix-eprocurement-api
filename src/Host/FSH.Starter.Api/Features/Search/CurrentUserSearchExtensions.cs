using System.Security.Claims;
using FSH.Framework.Core.Context;
using FSH.Framework.Shared.Constants;

namespace FSH.Starter.Api.Features.Search;

internal static class CurrentUserSearchExtensions
{
    public static Guid? GetVendorId(this ICurrentUser currentUser)
    {
        var claim = currentUser.GetUserClaims()?.FirstOrDefault(c => c.Type == ClaimConstants.VendorId);
        return Guid.TryParse(claim?.Value, out var vendorId) ? vendorId : null;
    }
}
