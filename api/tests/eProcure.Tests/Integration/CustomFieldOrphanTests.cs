using eProcure.Domain.CustomFields;
using eProcure.Domain.Procurement;
using eProcure.Domain.Suppliers;
using eProcure.Domain.Views;
using eProcure.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// The polymorphic (RecordType, RecordId) cost, guarded (D5 Step 0(d), ruled): no owning
/// aggregate hard-deletes today, so THIS query is the standing orphan guard — any future
/// delete flow that strands custom values turns it red and thereby inherits the cleanup
/// obligation. The probe is the exact query a cleanup job would use.
/// </summary>
public sealed class CustomFieldOrphanTests
{
    /// <summary>Custom values whose record no longer exists, per record type.</summary>
    private static async Task<int> OrphanCount(AppDbContext db)
    {
        var orphans = 0;
        foreach (var group in await db.CustomFieldValues.AsNoTracking().GroupBy(v => v.RecordType).ToListAsync())
        {
            var ids = group.Select(v => v.RecordId).ToHashSet();
            HashSet<Guid> live = group.Key switch
            {
                RecordType.Requisition => (await db.PurchaseRequisitions.Select(x => x.Id).ToListAsync()).ToHashSet(),
                RecordType.Rfq => (await db.Rfqs.Select(x => x.Id).ToListAsync()).ToHashSet(),
                RecordType.PurchaseOrder => (await db.PurchaseOrders.Select(x => x.Id).ToListAsync()).ToHashSet(),
                RecordType.Invoice => (await db.Invoices.Select(x => x.Id).ToListAsync()).ToHashSet(),
                RecordType.Asn => (await db.Asns.Select(x => x.Id).ToListAsync()).ToHashSet(),
                RecordType.Vendor => (await db.Vendors.Select(x => x.Id).ToListAsync()).ToHashSet(),
                RecordType.Onboarding => (await db.VendorOnboardingApplications.Select(x => x.Id).ToListAsync()).ToHashSet(),
                _ => [],
            };
            orphans += ids.Count(id => !live.Contains(id));
        }
        return orphans;
    }

    [Fact]
    public async Task Orphan_probe_flags_values_whose_record_is_gone_and_passes_clean_data()
    {
        var c = TestContext.New();
        var po = new PurchaseOrder { Code = "PO-D5-01", VendorId = Guid.NewGuid(), CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow };
        var def = new CustomFieldDef
        {
            Code = "cf_d5_probe", Label = "Probe", RecordType = RecordType.PurchaseOrder,
            DataType = CustomFieldDataType.Text, CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow,
        };
        c.Db.PurchaseOrders.Add(po);
        c.Db.CustomFieldDefs.Add(def);
        c.Db.CustomFieldValues.Add(new CustomFieldValue
        {
            FieldDefId = def.Id, RecordType = RecordType.PurchaseOrder, RecordId = po.Id,
            DataType = CustomFieldDataType.Text, ValueText = "attached", UpdatedUtc = c.Clock.UtcNow,
        });
        await c.Db.SaveChangesAsync();

        (await OrphanCount(c.Db)).Should().Be(0, "a value attached to a living record is not an orphan");

        c.Db.PurchaseOrders.Remove(po);                    // simulate a future hard-delete flow that forgets cleanup
        await c.Db.SaveChangesAsync();

        (await OrphanCount(c.Db)).Should().Be(1, "the stranded value must be flagged — the delete flow owes cleanup");
    }
}
