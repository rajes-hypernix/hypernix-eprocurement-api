using FSH.Modules.Suppliers.Data;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Vendors;

/// <summary>
/// Non-blocking duplicate-vendor detection shared by manual vendor creation and onboarding
/// approval: a match on RegistrationNo or exact Name never blocks the write, it only surfaces a
/// warning for the caller to review.
/// </summary>
internal static class VendorDuplicateChecker
{
    internal static async Task<string?> FindWarningAsync(SuppliersDbContext dbContext, string registrationNo, string name, CancellationToken cancellationToken)
    {
        bool hasRegistrationNo = !string.IsNullOrWhiteSpace(registrationNo) && registrationNo != "—";

        var existing = await dbContext.Vendors
            .AsNoTracking()
            .Where(v => (hasRegistrationNo && v.RegistrationNo == registrationNo) || v.Name == name)
            .Select(v => new { v.Code, v.RegistrationNo })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return null;
        }

        string field = hasRegistrationNo && existing.RegistrationNo == registrationNo ? "registration number" : "name";
        return $"A vendor with the same {field} already exists ({existing.Code}).";
    }
}
