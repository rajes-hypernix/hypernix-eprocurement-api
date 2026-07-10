using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice A — integration tests (EF InMemory): the derived PR header status persists/round-trips,
/// PrLineSourcing links are retained after closing (E3), and AuditEntry carries the typed
/// transition columns (DATA-MODEL-ANALYTICS §1).
/// </summary>
public class PrFoundationPersistenceTests
{
    private static readonly DateTime T = new(2026, 6, 30, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DerivedHeaderStatus_PersistsAndRoundTrips()
    {
        await using var db = TestDb.NewContext();

        var pr = new PurchaseRequisition
        {
            Code = "PR-2026-9100",
            Submitted = true,
            Lines = [PrLine.Create("A", "a", 4, "Unit", 100m, PrLineStatus.Open),
                     PrLine.Create("B", "b", 2, "Unit", 50m, PrLineStatus.InRfq)],
        };
        var id = pr.Id;
        pr.RecomputeHeaderStatus();
        pr.HeaderStatus.Should().Be(PrHeaderStatus.PartiallySourced);
        db.PurchaseRequisitions.Add(pr);
        await db.SaveChangesAsync();

        // Reload from the store with a clean tracker — proves the derived status was persisted,
        // not just held in memory, and that each line round-trips with its stable grain id.
        db.ChangeTracker.Clear();
        var loaded = await db.PurchaseRequisitions.Include(p => p.Lines).FirstAsync(p => p.Id == id);
        loaded.HeaderStatus.Should().Be(PrHeaderStatus.PartiallySourced);
        loaded.Lines.Should().HaveCount(2);
        loaded.Lines.Should().OnlyContain(l => l.Id != Guid.Empty);
        loaded.DerivedValue.Should().Be(500m);   // 4*100 + 2*50
    }

    [Fact]
    public async Task SourcingLink_IsRetained_AfterReturned()
    {
        await using var db = TestDb.NewContext();
        var link = new PrLineSourcing(Guid.NewGuid(), Guid.NewGuid(), "ITEM-1", 10m, T);
        var linkId = link.Id;
        link.MarkReturned("no vendor quoted", T.AddDays(2));
        db.PrLineSourcings.Add(link);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loaded = await db.PrLineSourcings.FirstAsync(x => x.Id == linkId);
        loaded.Should().NotBeNull();                                 // never deleted (E3)
        loaded.LinkStatus.Should().Be(LinkStatus.Returned);
        loaded.ClosedUtc.Should().Be(T.AddDays(2));
        loaded.Reason.Should().Be("no vendor quoted");
        (await db.PrLineSourcings.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AuditEntry_CarriesTypedTransitionColumns()
    {
        var ctx = TestContext.New();
        var audit = ctx.Audit;
        await audit.WriteTransitionAsync("PrLine", "ITEM-1", "Line cancelled",
            fromState: PrLineStatus.Open.ToString(), toState: PrLineStatus.Cancelled.ToString(),
            reason: "duplicate");

        var entry = await ctx.Db.AuditEntries.FirstAsync();
        entry.FromState.Should().Be("Open");
        entry.ToState.Should().Be("Cancelled");
        entry.Reason.Should().Be("duplicate");
        entry.ActorId.Should().Be("u_faridah");
    }
}
