using FSH.Modules.Platform.Contracts.Services;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Domain;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Services;

/// <summary>Phase 7 saved-view row source for Vendor — the record type Suppliers owns. Keys are
/// PascalCase and must match <c>ViewsSeedData</c>'s native field registry rows exactly.</summary>
public sealed class SuppliersSavedViewRowSource(SuppliersDbContext dbContext, ISavedViewSupplementalDataService supplementalData)
    : ISavedViewRowSource
{
    public async Task<IReadOnlyList<IDictionary<string, object?>>> GetRowsAsync(string recordType, CancellationToken ct)
    {
        if (recordType != "Vendor")
            throw new NotSupportedException($"Suppliers does not provide saved-view rows for record type '{recordType}'.");

        var vendors = await dbContext.Vendors
            .AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var supplemental = await supplementalData
            .GetSupplementalFieldsAsync(recordType, [.. vendors.Select(v => v.Id)], ct)
            .ConfigureAwait(false);

        var rows = new List<IDictionary<string, object?>>(vendors.Count);
        foreach (var v in vendors)
        {
            Dictionary<string, object?> row = new()
            {
                ["Id"] = v.Id,
                ["Code"] = v.Code,
                ["Name"] = v.Name,
                ["Status"] = v.Status.ToString(),
                ["Type"] = VendorTypeParser.ToApi(v.Type),
                ["Region"] = v.Region,
                ["State"] = v.State,
                ["City"] = v.City,
                ["Rating"] = v.Rating,
                ["CreditLimit"] = v.CreditLimit,
            };

            if (supplemental.TryGetValue(v.Id, out var extra))
            {
                foreach (var (key, value) in extra)
                    row[key] = value;
            }

            rows.Add(row);
        }

        return rows;
    }
}
