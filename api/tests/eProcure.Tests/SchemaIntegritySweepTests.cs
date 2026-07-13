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

    private static string TrimCode(string s) => s.Length > 60 ? s[..60] : s;

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

    /// <summary>CF-FIX1-T8 (operator gate): a SUCCESSFUL insert of each of the 15 data types
    /// against the LIVE Postgres — proving the extended ExactlyOne/KindMatch CHECKs accept
    /// every type→column mapping, not merely that they reject wrong shapes.</summary>
    [Fact]
    public async Task Every_one_of_the_15_data_types_inserts_a_value_row_against_the_live_checks()
    {
        await using var db = Db();
        var defIds = new List<Guid>();
        var valueIds = new List<Guid>();
        try
        {
            foreach (var type in Enum.GetValues<eProcure.Domain.CustomFields.CustomFieldDataType>())
            {
                var def = new eProcure.Domain.CustomFields.CustomFieldDef
                {
                    Code = TrimCode($"cf_sweep15_{type.ToString().ToLowerInvariant()}_{Guid.NewGuid():N}"),
                    Label = $"Sweep15 {type}", RecordType = eProcure.Domain.Views.RecordType.Vendor,
                    DataType = type, Scope = "Header",
                    CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow,
                };
                db.CustomFieldDefs.Add(def);
                defIds.Add(def.Id);

                var v = new eProcure.Domain.CustomFields.CustomFieldValue
                {
                    FieldDefId = def.Id, RecordType = eProcure.Domain.Views.RecordType.Vendor,
                    RecordId = Guid.NewGuid(), DataType = type, UpdatedUtc = DateTime.UtcNow,
                };
                switch (type)
                {
                    case eProcure.Domain.CustomFields.CustomFieldDataType.Text:
                    case eProcure.Domain.CustomFields.CustomFieldDataType.LongText:
                    case eProcure.Domain.CustomFields.CustomFieldDataType.Email:
                    case eProcure.Domain.CustomFields.CustomFieldDataType.Telephone:
                    case eProcure.Domain.CustomFields.CustomFieldDataType.Image:
                    case eProcure.Domain.CustomFields.CustomFieldDataType.Document:
                        v.ValueText = "x"; break;
                    case eProcure.Domain.CustomFields.CustomFieldDataType.Hyperlink:
                        v.ValueText = "https://x.example"; v.ValueLabel = "X"; break;   // label = companion, NOT a value column
                    case eProcure.Domain.CustomFields.CustomFieldDataType.Int:
                    case eProcure.Domain.CustomFields.CustomFieldDataType.Decimal:
                    case eProcure.Domain.CustomFields.CustomFieldDataType.Percent:
                        v.ValueNumber = 1m; break;
                    case eProcure.Domain.CustomFields.CustomFieldDataType.Money:
                        v.ValueMoney = 1m; break;
                    case eProcure.Domain.CustomFields.CustomFieldDataType.Date:
                        v.ValueDate = new DateOnly(2026, 3, 15); break;
                    case eProcure.Domain.CustomFields.CustomFieldDataType.DateTime:
                        v.ValueDateTime = DateTime.UtcNow; break;
                    case eProcure.Domain.CustomFields.CustomFieldDataType.Bool:
                        v.ValueBool = true; break;
                    case eProcure.Domain.CustomFields.CustomFieldDataType.ListValue:
                        v.ValueListCode = "X"; break;
                }
                db.CustomFieldValues.Add(v);
                valueIds.Add(v.Id);
            }
            var act = async () => await db.SaveChangesAsync();
            await act.Should().NotThrowAsync("every one of the 15 types must satisfy BOTH live CHECK constraints");
            Enum.GetValues<eProcure.Domain.CustomFields.CustomFieldDataType>().Should().HaveCount(15, "the operator's full type set");
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"delete from \"CustomFieldValues\" where \"Id\" = any({valueIds})");
            await db.Database.ExecuteSqlAsync($"delete from \"CustomFieldDefs\" where \"Id\" = any({defIds})");
        }
    }
}
