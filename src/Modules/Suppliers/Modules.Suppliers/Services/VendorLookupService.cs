using FSH.Modules.Suppliers.Contracts.Services;
using FSH.Modules.Suppliers.Data;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Services;

public sealed class VendorLookupService(SuppliersDbContext dbContext) : IVendorLookupService
{
    public async Task<IReadOnlyDictionary<Guid, VendorLookupDto>> GetManyAsync(IReadOnlyList<Guid> vendorIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(vendorIds);
        if (vendorIds.Count == 0)
            return new Dictionary<Guid, VendorLookupDto>();

        return await dbContext.Vendors
            .AsNoTracking()
            .Where(v => vendorIds.Contains(v.Id))
            .Select(v => new VendorLookupDto(v.Id, v.Code, v.Name))
            .ToDictionaryAsync(v => v.Id, ct)
            .ConfigureAwait(false);
    }
}
