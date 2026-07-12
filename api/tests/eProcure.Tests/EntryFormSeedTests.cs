using eProcure.Application.Forms;
using eProcure.Application.Views;
using eProcure.Domain.Views;
using eProcure.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// D7 P1 pins. The seed-side half of the parity probe: the Standard PR Form's rows are
/// exactly PrForm's HEADER_SECTION (keys, labels, placeholders, order, Memo full-width) —
/// the web test proves the RENDER is byte-identical; this proves the DATA can't drift from
/// the registry or grow/shrink silently. Plus the Postgres-backed presence probe: the
/// migration actually seeded the def, its 7 fields and the 7 numbering schemes.
/// </summary>
public sealed class EntryFormSeedTests
{
    [Fact]
    public void Standard_pr_fields_address_live_requisition_registry_keys_only()
    {
        var registryKeys = FieldRegistrySeed.Rows
            .Where(r => r.RecordType == RecordType.Requisition)
            .Select(r => r.FieldKey).ToHashSet();
        foreach (var f in EntryFormSeed.StandardPrFields)
            registryKeys.Should().Contain(f.FieldKey, "a seeded form field must address a live registry key");
    }

    [Fact]
    public void Standard_pr_form_reproduces_header_section_exactly()
    {
        // PrForm HEADER_SECTION, pinned datum-by-datum: two 3-across rows + full-width Memo.
        var f = EntryFormSeed.StandardPrFields;
        f.Should().HaveCount(7);
        f.Select(x => x.FieldKey).Should().ContainInOrder(
            "Requestor", "Department", "Category", "Location", "Job", "RequiredDate", "Memo");
        f.All(x => x.FieldGroup == "Header" && x.Subtab is null).Should().BeTrue("one section, no subtabs — today's layout");
        f.Single(x => x.FieldKey == "Job").Label.Should().Be("Job / Cost ref");
        f.Single(x => x.FieldKey == "RequiredDate").Label.Should().Be("Required by");
        f.Single(x => x.FieldKey == "Memo").Label.Should().Be("Memo / Justification");
        f.Single(x => x.FieldKey == "Memo").FullWidth.Should().BeTrue("Memo renders full-width (OD-D7-4)");
        f.Where(x => x.FieldKey != "Memo").All(x => !x.FullWidth).Should().BeTrue();
        f.Single(x => x.FieldKey == "Department").Placeholder.Should().Be("e.g. Maintenance");
        f.Single(x => x.FieldKey == "Location").Placeholder.Should().Be("e.g. Bintulu Plant");
    }

    [Fact]
    public void Numbering_schemes_reproduce_todays_formats_for_all_seven_record_types()
    {
        EntryFormSeed.Schemes.Select(s => s.RecordType).Should().BeEquivalentTo(Enum.GetValues<RecordType>());
        EntryFormSeed.Schemes.All(s => s.YearSegment && s.Digits == 4).Should().BeTrue("today's format is {PREFIX}-{YEAR}-{0:D4} everywhere");
        EntryFormSeed.Schemes.Single(s => s.RecordType == RecordType.Vendor).Prefix.Should().Be("SWK-V");
        EntryFormSeed.Schemes.Single(s => s.RecordType == RecordType.Onboarding).Prefix.Should().Be("VOB");
        EntryFormSeed.Schemes.Single(s => s.RecordType == RecordType.PurchaseOrder).Prefix.Should().Be("PO");
    }

    private static string Conn =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=eprocure;Username=eprocure;Password=localdev";

    [Fact]
    public async Task Migration_seeded_the_standard_form_and_schemes_in_postgres()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Conn).Options);
        if (!await db.Database.CanConnectAsync())
            throw new InvalidOperationException("Probe requires the local Docker Postgres (docker compose up -d).");

        var formId = EntryFormSeed.FormId(EntryFormSeed.StandardPrFormCode);
        var def = await db.EntryFormDefs.AsNoTracking().SingleAsync(d => d.Id == formId);
        def.IsSystem.Should().BeTrue();
        def.RecordType.Should().Be(RecordType.Requisition);
        (await db.EntryFormFields.AsNoTracking().CountAsync(x => x.FormDefId == formId)).Should().Be(7);
        (await db.NumberingSchemes.AsNoTracking().CountAsync()).Should().Be(7);
    }
}
