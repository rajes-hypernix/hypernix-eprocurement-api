using FSH.Framework.Persistence;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Sourcing.Contracts.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Sourcing.Data;

public sealed class SourcingDbInitializer(
    SourcingDbContext dbContext,
    IRoleService roleService,
    ILogger<SourcingDbInitializer> logger) : IDbInitializer
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        if ((await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).Any())
        {
            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("[Sourcing] applied migrations");
        }
    }

    /// <summary>
    /// Seeds Buyer/Approver/TechEvaluator/CommEvaluator grants for Sourcing.
    /// Merges onto existing role permissions (same pattern as Platform/Procurement/Communication)
    /// so module seed order cannot wipe other modules' grants.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await MergeRolePermissionsAsync(
            "Buyer",
            "Buyer — creates requisitions, manages RFQs, submits awards.",
            [
                SourcingPermissions.Requisitions.View,
                SourcingPermissions.Requisitions.Manage,
                SourcingPermissions.Rfqs.View,
                SourcingPermissions.Rfqs.ManageDraft,
                SourcingPermissions.Rfqs.ManageLifecycle,
                SourcingPermissions.Rfqs.Invite,
                SourcingPermissions.Rfqs.Rescind,
                SourcingPermissions.Rfqs.Extend,
                SourcingPermissions.Evaluation.ViewOpening,
                SourcingPermissions.Evaluation.ViewTechnical,
                SourcingPermissions.Award.View,
                SourcingPermissions.Award.Submit,
                SourcingPermissions.Clarifications.View,
                SourcingPermissions.Clarifications.Send,
            ],
            cancellationToken).ConfigureAwait(false);

        await MergeRolePermissionsAsync(
            "Approver",
            "Approver — Delegation-of-Authority sign-off on awards.",
            [
                SourcingPermissions.Award.View,
                SourcingPermissions.Award.Approve,
            ],
            cancellationToken).ConfigureAwait(false);

        await MergeRolePermissionsAsync(
            "TechEvaluator",
            "Technical evaluator — scores sealed bids, blind to vendor identity.",
            [
                SourcingPermissions.Evaluation.OpenTechnical,
                SourcingPermissions.Evaluation.Score,
                SourcingPermissions.Evaluation.FinalizeTechnical,
                SourcingPermissions.Evaluation.ViewTechnical,
            ],
            cancellationToken).ConfigureAwait(false);

        await MergeRolePermissionsAsync(
            "CommEvaluator",
            "Commercial evaluator — opens the sealed commercial envelope.",
            [
                SourcingPermissions.Evaluation.OpenCommercial,
            ],
            cancellationToken).ConfigureAwait(false);

        // Vendor portal: bid handlers are VendorId-claim scoped AND permission-gated.
        await MergeRolePermissionsAsync(
            "Vendor",
            "Vendor — onboarding, bidding, clarifications, PO acknowledge, ASN, invoice submit.",
            [
                SourcingPermissions.Bids.ViewMine,
                SourcingPermissions.Bids.Respond,
                SourcingPermissions.Clarifications.View,
                SourcingPermissions.Clarifications.Send,
            ],
            cancellationToken).ConfigureAwait(false);
    }

    private async Task MergeRolePermissionsAsync(
        string roleName,
        string description,
        List<string> permissionsToAdd,
        CancellationToken cancellationToken)
    {
        var catalog = await roleService.GetRolesAsync(1, 100, search: null, cancellationToken).ConfigureAwait(false);
        var role = catalog.Items.FirstOrDefault(r => string.Equals(r.Name, roleName, StringComparison.Ordinal));
        if (role is null)
        {
            role = await roleService.CreateOrUpdateRoleAsync(string.Empty, roleName, description, cancellationToken)
                .ConfigureAwait(false);
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[Sourcing] seeded {Role} role", roleName);
        }

        var withPerms = await roleService.GetWithPermissionsAsync(role.Id, cancellationToken).ConfigureAwait(false);
        var merged = (withPerms.Permissions ?? [])
            .Concat(permissionsToAdd)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        await roleService.UpdatePermissionsAsync(role.Id, merged, cancellationToken).ConfigureAwait(false);
    }
}
