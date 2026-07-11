using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice H T8: the PrLineSourcing → PrLine foreign key (possible once T1 gave PrLine a stable key). It's
/// a DEFERRABLE DB-level FK (PrLine is owned, so EF models no navigation to it). Proves a sourcing row
/// pointing at a real PR line is accepted and one pointing at a missing line is rejected. Postgres-backed
/// (the FK is a relational guarantee), rolled back.
/// </summary>
public sealed class PrLineSourcingFkTests
{
    private static string Conn =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=eprocure;Username=eprocure;Password=localdev";

    private static AppDbContext NewCtx() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Conn).Options);

    private static readonly DateTime Now = new(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Sourcing_must_reference_a_real_pr_line()
    {
        await using var db = NewCtx();
        if (!await db.Database.CanConnectAsync())
            throw new InvalidOperationException("FK test requires the local Docker Postgres (docker compose up -d).");

        var sfx = Guid.NewGuid().ToString("N")[..8];
        await using var tx = await db.Database.BeginTransactionAsync();

        var pr = new PurchaseRequisition { Code = $"PR-FK-{sfx}", CreatedUtc = Now, UpdatedUtc = Now };
        pr.Lines.Add(PrLine.Create("ITEM", "desc", 5, "Unit", 10));
        var rfq = new Rfq { Code = $"RFQ-FK-{sfx}", Title = "FK", CreatedUtc = Now, UpdatedUtc = Now };
        db.PurchaseRequisitions.Add(pr);
        db.Rfqs.Add(rfq);
        await db.SaveChangesAsync();
        var realLineId = pr.Lines.Single().Id;

        // Legit sourcing → the deferred FK is satisfied when forced to check immediately.
        db.PrLineSourcings.Add(new PrLineSourcing(realLineId, rfq.Id, "L1", 5, Now));
        await db.SaveChangesAsync();
        var checkNow = () => db.Database.ExecuteSqlRaw("SET CONSTRAINTS ALL IMMEDIATE;");
        checkNow.Should().NotThrow("the sourcing points at a real PR line");

        // Orphan sourcing → rejected (missing PrLine). Constraints are now IMMEDIATE, so the insert throws.
        db.PrLineSourcings.Add(new PrLineSourcing(Guid.NewGuid(), rfq.Id, "L2", 5, Now));
        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("the PrLineSourcings.PrLineId FK rejects a dangling reference");

        await tx.RollbackAsync();
    }
}
