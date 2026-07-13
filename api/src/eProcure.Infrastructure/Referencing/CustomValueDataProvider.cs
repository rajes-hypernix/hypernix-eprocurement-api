using eProcure.Application.CustomFields;
using eProcure.Domain.CustomFields;
using eProcure.Domain.Views;
using eProcure.Infrastructure.Persistence;
using eProcure.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Referencing;

/// <summary>The primary value store: CustomFieldValues rows, joined to each record's
/// lifecycle (RecordLifecycle — fail-closed). Serves BOTH the field grain (all values of a
/// def) and the list-value grain (values holding a code of a bound list). Purge removes
/// ONLY allowlisted-historical rows and returns them for the mandatory audit snapshot.</summary>
public sealed class CustomValueDataProvider(AppDbContext db) : ICustomFieldDataProvider, ICustomListValueDataProvider
{
    public string StoreName => "Record values";

    public async Task<DataReferenceSummary> CountValuesAsync(Guid fieldDefId, CancellationToken ct = default)
    {
        var rows = await db.CustomFieldValues.AsNoTracking()
            .Where(v => v.FieldDefId == fieldDefId)
            .Select(v => new { v.RecordType, v.RecordId, v.LineId }).ToListAsync(ct);
        return await SummarizeAsync(rows.Select(r => (r.RecordType, r.RecordId, r.LineId)).ToList(), ct);
    }

    public async Task<PurgeSnapshot> PurgeHistoricalAsync(Guid fieldDefId, CancellationToken ct = default)
    {
        var rows = await db.CustomFieldValues
            .Where(v => v.FieldDefId == fieldDefId).ToListAsync(ct);
        return await PurgeRowsAsync(rows, ct);
    }

    public async Task<DataReferenceSummary> CountValueUsageAsync(Guid listId, string listCode, string valueCode, CancellationToken ct = default)
    {
        var rows = await ValueRowsHoldingAsync(listId, valueCode).AsNoTracking()
            .Select(v => new { v.RecordType, v.RecordId, v.LineId }).ToListAsync(ct);
        return await SummarizeAsync(rows.Select(r => (r.RecordType, r.RecordId, r.LineId)).ToList(), ct);
    }

    public async Task<PurgeSnapshot> PurgeHistoricalAsync(Guid listId, string listCode, string valueCode, CancellationToken ct = default)
    {
        var rows = await ValueRowsHoldingAsync(listId, valueCode).ToListAsync(ct);
        return await PurgeRowsAsync(rows, ct);
    }

    private IQueryable<CustomFieldValue> ValueRowsHoldingAsync(Guid listId, string valueCode) =>
        db.CustomFieldValues.Where(v => v.ValueListCode == valueCode
            && db.CustomFieldDefs.Any(d => d.Id == v.FieldDefId && d.CustomListId == listId));

    private async Task<DataReferenceSummary> SummarizeAsync(
        IReadOnlyList<(RecordType RecordType, Guid RecordId, Guid? LineId)> rows, CancellationToken ct)
    {
        var counts = new List<DataTypeCount>();
        var live = 0; var historical = 0;
        foreach (var grp in rows.GroupBy(r => r.RecordType))
        {
            var map = await RecordLifecycle.ClassifyAsync(db, grp.Key,
                grp.Select(r => (r.RecordId, r.LineId)).ToList(), ct);
            var h = grp.Count(r => map.GetValueOrDefault((r.RecordId, r.LineId)));
            var l = grp.Count() - h;
            counts.Add(new DataTypeCount(grp.Key.ToString(), l, h));
            live += l; historical += h;
        }
        return new DataReferenceSummary(StoreName, live, historical, counts);
    }

    private async Task<PurgeSnapshot> PurgeRowsAsync(List<CustomFieldValue> rows, CancellationToken ct)
    {
        var removed = new List<PurgedValue>();
        foreach (var grp in rows.GroupBy(r => r.RecordType))
        {
            var map = await RecordLifecycle.ClassifyAsync(db, grp.Key,
                grp.Select(r => (r.RecordId, (Guid?)r.LineId)).ToList(), ct);
            foreach (var row in grp)
            {
                if (!map.GetValueOrDefault((row.RecordId, row.LineId))) continue;   // Live/unknown → NEVER purge
                removed.Add(new PurgedValue(row.RecordType.ToString(), row.RecordId, row.LineId,
                    await RecordLabelAsync(row.RecordType, row.RecordId, ct), CustomFieldService.Render(row)));
                db.CustomFieldValues.Remove(row);
            }
        }
        return new PurgeSnapshot(StoreName, removed);
    }

    private async Task<string> RecordLabelAsync(RecordType type, Guid id, CancellationToken ct) => type switch
    {
        RecordType.PurchaseOrder => await db.PurchaseOrders.AsNoTracking().Where(p => p.Id == id).Select(p => p.Code).FirstOrDefaultAsync(ct) ?? id.ToString(),
        RecordType.Invoice => await db.Invoices.AsNoTracking().Where(i => i.Id == id).Select(i => i.Code).FirstOrDefaultAsync(ct) ?? id.ToString(),
        RecordType.Requisition => await db.PurchaseRequisitions.AsNoTracking().Where(p => p.Id == id).Select(p => p.Code).FirstOrDefaultAsync(ct) ?? id.ToString(),
        RecordType.Rfq => await db.Rfqs.AsNoTracking().Where(r => r.Id == id).Select(r => r.Code).FirstOrDefaultAsync(ct) ?? id.ToString(),
        _ => id.ToString(),
    };
}

/// <summary>Native code-holding master-data columns (the A2F probes, now a PROVIDER):
/// e.g. Vendor.Country/State/City/Currency/PaymentTerms/Bank storing a list value's code.
/// Master data is NEVER historical — any usage is Live, so it blocks deletion and there
/// is nothing to purge here by construction.</summary>
public sealed class NativeCodeColumnDataProvider(AppDbContext db) : ICustomListValueDataProvider
{
    public string StoreName => "Master data columns";

    public async Task<DataReferenceSummary> CountValueUsageAsync(Guid listId, string listCode, string valueCode, CancellationToken ct = default)
    {
        // The A2F native probes, verbatim — master-data / event usage is ALWAYS Live.
        var live = listCode switch
        {
            "COUNTRY" => await db.Vendors.AsNoTracking().CountAsync(v => v.Country == valueCode || v.Addresses.Any(a => a.Country == valueCode), ct),
            "STATE" => await db.Vendors.AsNoTracking().CountAsync(v => v.State == valueCode || v.Addresses.Any(a => a.State == valueCode), ct),
            "CITY" => await db.Vendors.AsNoTracking().CountAsync(v => v.City == valueCode || v.Addresses.Any(a => a.City == valueCode), ct),
            "BANK" => await db.Vendors.AsNoTracking().CountAsync(v => v.BankAccounts.Any(b => b.Bank == valueCode), ct),
            "CURRENCY" => await db.Vendors.AsNoTracking().CountAsync(v => v.Currencies.Any(c => c.Code == valueCode), ct),
            "PAYMENT_TERMS" => await db.Vendors.AsNoTracking().CountAsync(v => v.PaymentTerms == valueCode, ct),
            "RFQ_DECLINE_REASON" or "RFQ_RESCIND_REASON" or "RFQ_EXTENSION_REASON" =>
                await db.RfqEvents.AsNoTracking().CountAsync(e => e.ReasonCode == valueCode, ct)
                + await db.Rfqs.AsNoTracking().CountAsync(r => r.Invitations.Any(i => i.DeclineReasonCode == valueCode || i.RescindReasonCode == valueCode), ct),
            _ => 0,
        };
        return new DataReferenceSummary(StoreName, live, 0,
            live > 0 ? [new DataTypeCount(listCode.StartsWith("RFQ_") ? "Rfq" : "Vendor", live, 0)] : []);
    }

    public Task<PurgeSnapshot> PurgeHistoricalAsync(Guid listId, string listCode, string valueCode, CancellationToken ct = default) =>
        Task.FromResult(new PurgeSnapshot(StoreName, []));   // master data: nothing is ever historical
}
