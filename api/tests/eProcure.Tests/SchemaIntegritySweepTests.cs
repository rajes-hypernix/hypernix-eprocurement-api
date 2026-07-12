using eProcure.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// TEST-SWEEP-T2 (FULL-TEST-INVENTORY PART 14): schema-level facts pinned against the LIVE
/// Postgres — the FK floor, the sparse custom-value CHECKs, and the xmin concurrency tokens.
/// These are the inventory rows that had no explicit assertion (FK count, CHECK presence,
/// "9 aggregates" breadth) — each is now machine-checked, not hoped against.
/// </summary>
public sealed class SchemaIntegritySweepTests
{
    private static string Conn =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=eprocure;Username=eprocure;Password=localdev";

    private static AppDbContext Db() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Conn).Options);

    [Fact]
    public async Task Foreign_key_count_holds_the_63_floor()
    {
        await using var db = Db();
        var fkCount = await db.Database.SqlQuery<int>(
            $"select count(*)::int as \"Value\" from information_schema.table_constraints where constraint_type = 'FOREIGN KEY' and table_schema = 'public'")
            .SingleAsync();
        fkCount.Should().BeGreaterThanOrEqualTo(63,
            "the audited FK floor — lifecycle references stay database-enforced, never drift to soft references");
    }

    [Fact]
    public async Task Custom_value_CHECKs_exist_and_a_two_column_row_is_rejected()
    {
        await using var db = Db();
        var checks = await db.Database.SqlQuery<string>(
            $"select conname as \"Value\" from pg_constraint where contype = 'c' and conrelid = '\"CustomFieldValues\"'::regclass")
            .ToListAsync();
        checks.Should().NotBeEmpty("the sparse typed-column storage is guarded by DB CHECKs, not service discipline alone");

        // A row with TWO populated value columns must be rejected by the CHECK itself.
        var defId = Guid.NewGuid();
        var act = async () => await db.Database.ExecuteSqlAsync($@"
            insert into ""CustomFieldValues""
                (""Id"", ""FieldDefId"", ""RecordType"", ""RecordId"", ""DataType"",
                 ""ValueText"", ""ValueNumber"", ""ValueMoney"", ""ValueDate"", ""ValueBool"", ""ValueListCode"", ""UpdatedUtc"")
            values ({Guid.NewGuid()}, {defId}, 'PurchaseOrder', {Guid.NewGuid()}, 'Text',
                 'two', {1m}, null, null, null, null, {DateTime.UtcNow})");
        (await act.Should().ThrowAsync<Npgsql.PostgresException>("exactly one value column may be populated"))
            .Which.SqlState.Should().Be("23514");   // check_violation
    }

    [Fact]
    public void Xmin_concurrency_token_is_mapped_on_every_concurrency_bearing_aggregate()
    {
        using var db = Db();
        // The Slice-set aggregates that carry optimistic concurrency (the "9 aggregates" row).
        string[] expected =
        [
            "Rfq", "PurchaseRequisition", "PurchaseOrder", "Invoice", "Asn", "Grn",
            "Award", "Vendor", "VendorOnboardingApplication",
        ];
        var missing = new List<string>();
        foreach (var name in expected)
        {
            var et = db.Model.GetEntityTypes().SingleOrDefault(e => e.ClrType.Name == name);
            if (et is null) { missing.Add($"{name} (entity not found)"); continue; }
            var hasXmin = et.GetProperties().Any(p =>
                p.IsConcurrencyToken && string.Equals(p.GetColumnName(), "xmin", StringComparison.OrdinalIgnoreCase));
            if (!hasXmin) missing.Add(name);
        }
        missing.Should().BeEmpty("every mutation-bearing aggregate maps xmin as its concurrency token (409 via middleware is proven in ConcurrencyTests)");
    }
}
