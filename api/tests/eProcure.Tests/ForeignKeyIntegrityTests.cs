using eProcure.Domain.Procurement;
using eProcure.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// DBA-1: the P2P foreign keys are a RELATIONAL guarantee, so they can only be proven against real
/// Postgres (the in-memory provider ignores FK constraints). These run against the local Docker DB
/// inside a rolled-back transaction (no data is left behind) and skip cleanly if it is not reachable.
/// </summary>
public sealed class ForeignKeyIntegrityTests
{
    private static string Conn =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=eprocure;Username=eprocure;Password=localdev";

    private static AppDbContext NewCtx() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Conn).Options);

    private static readonly DateTime Now = new(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);

    private static async Task RequireDb(AppDbContext db)
    {
        if (!await db.Database.CanConnectAsync())
            throw new InvalidOperationException("FK enforcement tests require the local Docker Postgres (docker compose up -d).");
    }

    private static PurchaseOrder NewPo(Guid vendorId, Guid? rfqId) => new()
    {
        Code = "PO-FKTEST-" + Guid.NewGuid().ToString("N")[..8],
        VendorId = vendorId, RfqId = rfqId, Status = PoStatus.Draft, CreatedUtc = Now, UpdatedUtc = Now,
    };

    [Fact]
    public async Task Orphan_purchase_order_insert_is_rejected_by_the_foreign_key()
    {
        await using var db = NewCtx();
        await RequireDb(db);
        await using var tx = await db.Database.BeginTransactionAsync();
        db.PurchaseOrders.Add(NewPo(vendorId: Guid.NewGuid(), rfqId: null));   // vendor does not exist

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("the PurchaseOrders.VendorId FK rejects a dangling reference");

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Legitimate_purchase_order_insert_succeeds()
    {
        await using var db = NewCtx();
        await RequireDb(db);
        var vendorId = await db.Vendors.Select(v => v.Id).FirstAsync();

        await using var tx = await db.Database.BeginTransactionAsync();
        db.PurchaseOrders.Add(NewPo(vendorId, rfqId: null));   // real vendor, no RFQ (RfqId is nullable)

        var act = async () => await db.SaveChangesAsync();
        await act.Should().NotThrowAsync("a PO pointing at a real vendor satisfies every FK");

        await tx.RollbackAsync();   // leave the dev DB untouched
    }
}
