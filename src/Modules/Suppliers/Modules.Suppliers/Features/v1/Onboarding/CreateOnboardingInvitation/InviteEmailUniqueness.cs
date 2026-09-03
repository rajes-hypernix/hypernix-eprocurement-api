using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Domain.Onboarding;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.CreateOnboardingInvitation;

/// <summary>
/// One email may only belong to one vendor relationship: Identity login, vendor portal user,
/// vendor contact, or an in-flight onboarding application.
/// </summary>
internal static class InviteEmailUniqueness
{
    internal static async Task EnsureAvailableAsync(
        SuppliersDbContext dbContext,
        IUserService users,
        string email,
        CancellationToken cancellationToken)
    {
        string trimmed = email.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return;

        if (await users.ExistsWithEmailAsync(trimmed, exceptId: null, cancellationToken).ConfigureAwait(false))
        {
            throw new OnboardingRuleException(
                "This email is already registered as a user and cannot be invited as a vendor.");
        }

        bool vendorLogin = await dbContext.VendorUsers.AsNoTracking()
            .AnyAsync(u => u.Email.Equals(trimmed, StringComparison.OrdinalIgnoreCase), cancellationToken)
            .ConfigureAwait(false);
        if (vendorLogin)
        {
            throw new OnboardingRuleException(
                "This email already belongs to a vendor login and cannot be invited again.");
        }

        bool vendorContact = await dbContext.Vendors.AsNoTracking()
            .AnyAsync(v => v.Contacts.Any(c => c.Email.Equals(trimmed, StringComparison.OrdinalIgnoreCase)), cancellationToken)
            .ConfigureAwait(false);
        if (vendorContact)
        {
            throw new OnboardingRuleException(
                "This email is already a contact on a vendor and cannot be invited for another vendor.");
        }

        bool liveApplication = await dbContext.VendorOnboardingApplications.AsNoTracking()
            .AnyAsync(
                a => a.Status != OnboardingStatus.Rejected
                    && a.Status != OnboardingStatus.Revoked
                    && a.Status != OnboardingStatus.Withdrawn
                    && (a.Email.Equals(trimmed, StringComparison.OrdinalIgnoreCase)
                        || a.Contacts.Any(c => c.Email.Equals(trimmed, StringComparison.OrdinalIgnoreCase))),
                cancellationToken)
            .ConfigureAwait(false);
        if (liveApplication)
        {
            throw new OnboardingRuleException(
                "This email already has an onboarding application. Resend that invitation instead of creating another.");
        }
    }
}
