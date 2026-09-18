using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Sourcing.Contracts.Authorization;

namespace FSH.Modules.Sourcing.Services;

/// <summary>
/// Bid Openings list/detail: CommEvaluator is seeded with OpenCommercial (not always ViewOpening).
/// RequirePermission only checks the first grant, so these GETs authorize any envelope permission.
/// Unseal/score commands stay on their own permissions + RFQ assignment.
/// </summary>
internal static class BidOpeningAccess
{
    internal static async Task<bool> CanViewAsync(
        IUserService userService,
        string userId,
        CancellationToken cancellationToken)
    {
        return await userService.HasPermissionAsync(userId, SourcingPermissions.Evaluation.ViewOpening, cancellationToken).ConfigureAwait(false)
            || await userService.HasPermissionAsync(userId, SourcingPermissions.Evaluation.OpenCommercial, cancellationToken).ConfigureAwait(false)
            || await userService.HasPermissionAsync(userId, SourcingPermissions.Evaluation.OpenTechnical, cancellationToken).ConfigureAwait(false)
            || await userService.HasPermissionAsync(userId, SourcingPermissions.Evaluation.Score, cancellationToken).ConfigureAwait(false);
    }
}
