using eProcure.Domain.Procurement;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice H T1 (DBA-5): the nine owned line collections must carry a stable <c>Guid Id</c> grain key
/// (the <c>PrLine</c> precedent) so they are usable as fact-table grain. The model test proves every
/// line type is keyed by a single Guid "Id"; the round-trip proves that key is STABLE across a parent
/// mutation (an ordinal shadow key would renumber — a real grain key must not).
/// </summary>
public sealed class StableLineKeysTests
{
    private static string Conn =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=eprocure;Username=eprocure;Password=localdev";

    private static AppDbContext NewCtx() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Conn).Options);

    private static readonly DateTime Now = new(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

    public static readonly TheoryData<Type> LineTypes = new()
    {
        typeof(RfqLine), typeof(BidLine), typeof(BidAnswer), typeof(BidAttachment),
        typeof(PoLine), typeof(AsnLine), typeof(GrnLine), typeof(InvoiceLine), typeof(AwardAllocation),
    };

    [Theory]
    [MemberData(nameof(LineTypes))]
    public void Every_owned_line_type_is_keyed_by_a_single_Guid_Id(Type lineType)
    {
        // Reads the built model only — no database connection needed.
        using var db = NewCtx();
        var entity = db.Model.GetEntityTypes().Single(e => e.ClrType == lineType);

        var pk = entity.FindPrimaryKey();
        pk.Should().NotBeNull($"{lineType.Name} must have a primary key");
        pk!.Properties.Should().ContainSingle($"{lineType.Name} must be keyed by a single column")
            .Which.Name.Should().Be("Id");
        pk.Properties[0].ClrType.Should().Be(typeof(Guid), $"{lineType.Name}.Id must be a Guid grain key");
    }

    [Fact]
    public async Task Line_keys_are_stable_across_a_parent_mutation()
    {
        await using var db = NewCtx();
        if (!await db.Database.CanConnectAsync())
            throw new InvalidOperationException("Stable-key round-trip requires the local Docker Postgres (docker compose up -d).");

        var sfx = Guid.NewGuid().ToString("N")[..8];
        await using var tx = await db.Database.BeginTransactionAsync();

        var vendor = new Vendor { Code = $"V-SLK-{sfx}", Name = "SLK", RegisteredName = "SLK" };
        var rfq = new Rfq { Code = $"RFQ-SLK-{sfx}", Title = "SLK", CreatedUtc = Now, UpdatedUtc = Now };
        db.Vendors.Add(vendor);
        db.Rfqs.Add(rfq);
        var bid = new Bid
        {
            Code = $"BID-SLK-{sfx}", RfqId = rfq.Id, VendorId = vendor.Id, CreatedUtc = Now, UpdatedUtc = Now,
            Lines = { new BidLine { ItemCode = "A" }, new BidLine { ItemCode = "B" } },
            Answers = { new BidAnswer { QuestionOrder = 0, Value = "x" } },
        };
        db.Bids.Add(bid);
        await db.SaveChangesAsync();

        // The keys assigned at insert are real Guids, unique, and persisted.
        var beforeIds = await db.Bids.AsNoTracking().Where(x => x.Id == bid.Id)
            .SelectMany(x => x.Lines).Select(l => new { l.ItemCode, l.Id })
            .OrderBy(l => l.ItemCode).ToListAsync();
        beforeIds.Should().OnlyContain(l => l.Id != Guid.Empty);
        beforeIds.Select(l => l.Id).Should().OnlyHaveUniqueItems();

        // A grain key must be addressable: a line is retrievable BY its Guid.
        var lineAId = beforeIds.Single(l => l.ItemCode == "A").Id;
        var byKey = await db.Bids.AsNoTracking().Where(x => x.Id == bid.Id)
            .SelectMany(x => x.Lines).SingleAsync(l => l.Id == lineAId);
        byKey.ItemCode.Should().Be("A");

        // Mutate the aggregate (update an existing line) and persist — the keys must NOT change
        // (an ordinal shadow key can be renumbered on rewrite; a real grain key is stable).
        var reload = await db.Bids.Include(x => x.Lines).FirstAsync(x => x.Id == bid.Id);
        reload.Lines.Single(l => l.ItemCode == "A").Price = 999m;
        await db.SaveChangesAsync();

        var afterIds = await db.Bids.AsNoTracking().Where(x => x.Id == bid.Id)
            .SelectMany(x => x.Lines).Select(l => new { l.ItemCode, l.Id })
            .OrderBy(l => l.ItemCode).ToListAsync();
        afterIds.Should().BeEquivalentTo(beforeIds, "every line's grain key is stable across a write to the aggregate");

        await tx.RollbackAsync();
    }
}
