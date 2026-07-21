using FSH.Modules.Suppliers.Contracts.Services;
using FSH.Modules.Suppliers.Data;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Services;

public sealed class VendorPortalUserDirectory(SuppliersDbContext dbContext) : IVendorPortalUserDirectory
{
    public async Task<IReadOnlyList<string>> GetIdentityUserIdsForVendorsAsync(
        IReadOnlyList<Guid> vendorIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vendorIds);
        if (vendorIds.Count == 0)
            return [];

        return await dbContext.VendorUsers
            .AsNoTracking()
            .Where(u => vendorIds.Contains(u.VendorId) && u.IdentityUserId != null)
            .Select(u => u.IdentityUserId!)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
