using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice H T6 (AN-5/AN-7): the country-conformance backfill collapses the free-text/code split. A
/// vendor stored as the LABEL "Malaysia" (the pre-T6 seed default) and a vendor stored as the CODE
/// "MY" (what CreateManual/onboarding wrote) are TWO GROUP BY buckets for the same country; after the
/// shared backfill they are one. Runs the SAME statements the migration runs, Postgres-backed (needs
/// the seeded COUNTRY Custom List), rolled back.
/// </summary>
public sealed class ConformedVocabularyBackfillTests
{
    private static string Conn =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=eprocure;Username=eprocure;Password=localdev";

    private static AppDbContext NewCtx() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Conn).Options);

    private static readonly DateTime Now = new(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Backfill_conforms_the_label_bucket_to_the_code_and_merges_the_split()
    {
        await using var db = NewCtx();
        if (!await db.Database.CanConnectAsync())
            throw new InvalidOperationException("Conformance test requires the local Docker Postgres (docker compose up -d).");

        var sfx = Guid.NewGuid().ToString("N")[..8];
        await using var tx = await db.Database.BeginTransactionAsync();

        var vLabel = new Vendor   // pre-T6 seed shape: free-text label
        {
            Code = $"V-LBL-{sfx}", Name = "LabelCo", RegisteredName = "LabelCo", Country = "Malaysia",
            Addresses = { new VendorAddress { Type = "Registered", Country = "Malaysia", IsPrimary = true } },
        };
        var vCode = new Vendor    // CreateManual/onboarding shape: already the code
        {
            Code = $"V-COD-{sfx}", Name = "CodeCo", RegisteredName = "CodeCo", Country = "MY",
        };
        db.Vendors.AddRange(vLabel, vCode);
        await db.SaveChangesAsync();

        var ids = new[] { vLabel.Id, vCode.Id };
        var before = await db.Vendors.AsNoTracking().Where(v => ids.Contains(v.Id)).Select(v => v.Country).Distinct().CountAsync();
        before.Should().Be(2, "the same country is split across a label bucket and a code bucket");

        foreach (var sql in ConformedVocabularyBackfill.Up)
            await db.Database.ExecuteSqlRawAsync(sql);

        var after = await db.Vendors.AsNoTracking().Where(v => ids.Contains(v.Id)).Select(v => v.Country).Distinct().ToListAsync();
        after.Should().BeEquivalentTo(["MY"], "both vendors now carry the conformed ISO-2 code — one bucket");
        var addr = await db.Vendors.AsNoTracking().Where(v => v.Id == vLabel.Id).SelectMany(v => v.Addresses).Select(a => a.Country).SingleAsync();
        addr.Should().Be("MY", "the address label is conformed too");

        // Down restores the free-text label for the row that held one.
        foreach (var sql in ConformedVocabularyBackfill.Down)
            await db.Database.ExecuteSqlRawAsync(sql);
        (await db.Vendors.AsNoTracking().Where(v => v.Id == vLabel.Id).Select(v => v.Country).SingleAsync()).Should().Be("Malaysia");

        await tx.RollbackAsync();
    }
}
