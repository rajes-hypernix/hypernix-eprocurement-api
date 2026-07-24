using FSH.Modules.Platform.Contracts.Services;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Suppliers.Contracts.Services;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Services;

/// <summary>Phase 7 saved-view row source for PurchaseOrder — the record type Procurement owns.
/// Keys are PascalCase and must match <c>ViewsSeedData</c>'s native field registry rows exactly.
/// <c>VendorName</c> is a one-hop related field, resolved via Suppliers' whitelist-only
/// <see cref="IVendorLookupService"/> (Name/Code only — see that interface's own doc comment for
/// why bank details can never reach this seam) rather than a raw <c>VendorId</c>.</summary>
public sealed class ProcurementSavedViewRowSource(
    ProcurementDbContext dbContext,
    IVendorLookupService vendorLookup,
    ISavedViewSupplementalDataService supplementalData) : ISavedViewRowSource
{
    public async Task<IReadOnlyList<IDictionary<string, object?>>> GetRowsAsync(string recordType, CancellationToken ct)
    {
        if (recordType != "PurchaseOrder")
            throw new NotSupportedException($"Procurement does not provide saved-view rows for record type '{recordType}'.");

        var orders = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Lines)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var vendors = await vendorLookup
            .GetManyAsync([.. orders.Select(p => p.VendorId).Distinct()], ct)
            .ConfigureAwait(false);

        var supplemental = await supplementalData
            .GetSupplementalFieldsAsync(recordType, [.. orders.Select(p => p.Id)], ct)
            .ConfigureAwait(false);

        var rows = new List<IDictionary<string, object?>>(orders.Count);
        foreach (var po in orders)
        {
            Dictionary<string, object?> row = new()
            {
                ["Id"] = po.Id,
                ["Code"] = po.Code,
                ["Status"] = po.Status.ToString(),
                ["SourceKind"] = po.SourceKind.ToString(),
                ["Currency"] = po.Currency,
                ["VendorName"] = vendors.TryGetValue(po.VendorId, out var vendor) ? vendor.Name : null,
                ["TotalValue"] = po.TotalValue,
                ["RequiredDate"] = po.RequiredDate,
                ["DeliveryDate"] = po.DeliveryDate,
                ["IssuedUtc"] = po.IssuedUtc,
            };

            if (supplemental.TryGetValue(po.Id, out var extra))
            {
                foreach (var (key, value) in extra)
                    row[key] = value;
            }

            rows.Add(row);
        }

        return rows;
    }
}
