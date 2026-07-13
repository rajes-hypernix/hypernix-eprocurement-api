using eProcure.Application.Forms;
using eProcure.Domain.Views;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// D7 numbering pins (OD-D7-6): the scheme shapes the FORMAT only — the gap-free
/// sequence machinery is untouched (CodeGeneratorConcurrencyTests keeps pinning PRG-2)
/// and, because sequences are keyed (prefix, bucket) and never reset, ALL FOUR
/// format-change cases preserve uniqueness by construction: (1) prefix change starts a
/// fresh counter; (2) reverting REATTACHES to the preserved counter — never re-issues;
/// (3) digits change continues the same counter, padded differently; (4) YearSegment=false
/// buckets under year 0, so year-less codes cannot collide across years.
/// </summary>
public sealed class NumberingTests
{
    private static (TestContext C, NumberingService Svc) New()
    {
        var c = TestContext.New();
        return (c, new NumberingService(c.Db, c.Clock));
    }

    private static Task<NumberingSchemeDto> Set(NumberingService svc, string prefix, bool year = true, int digits = 4) =>
        svc.UpdateAsync("PurchaseOrder", new SaveNumberingSchemeRequest(prefix, year, digits));

    [Fact]
    public async Task Seeded_schemes_mint_todays_formats_verbatim()
    {
        var (c, _) = New();
        (await c.Codes.NextAsync(RecordType.PurchaseOrder)).Should().Be("PO-2026-0001");
        (await c.Codes.NextAsync(RecordType.Vendor)).Should().Be("SWK-V-2026-0001");
        (await c.Codes.NextAsync("GRN")).Should().Be("GRN-2026-0001", "system artifacts stay literal");
    }

    [Fact]
    public async Task Format_change_shapes_the_next_code_and_history_is_untouched()
    {
        var (c, svc) = New();
        var before = await c.Codes.NextAsync(RecordType.PurchaseOrder);   // PO-2026-0001
        await Set(svc, "SPO", digits: 5);
        var after = await c.Codes.NextAsync(RecordType.PurchaseOrder);
        after.Should().Be("SPO-2026-00001", "the scheme is consulted at format time");
        before.Should().Be("PO-2026-0001", "minted codes are strings on records — never rewritten");
    }

    [Fact]
    public async Task Case1_and_2_prefix_change_starts_fresh_and_reverting_reattaches_never_reissuing()
    {
        var (c, svc) = New();
        (await c.Codes.NextAsync(RecordType.PurchaseOrder)).Should().Be("PO-2026-0001");
        (await c.Codes.NextAsync(RecordType.PurchaseOrder)).Should().Be("PO-2026-0002");
        await Set(svc, "SPO");
        (await c.Codes.NextAsync(RecordType.PurchaseOrder)).Should().Be("SPO-2026-0001", "a new prefix is a new counter — distinct codes by prefix");
        await Set(svc, "PO");
        (await c.Codes.NextAsync(RecordType.PurchaseOrder)).Should().Be("PO-2026-0003", "reverting REATTACHES to the preserved counter — 0001/0002 are never re-issued");
    }

    [Fact]
    public async Task Case3_digits_change_continues_the_same_counter()
    {
        var (c, svc) = New();
        (await c.Codes.NextAsync(RecordType.PurchaseOrder)).Should().Be("PO-2026-0001");
        await Set(svc, "PO", digits: 6);
        (await c.Codes.NextAsync(RecordType.PurchaseOrder)).Should().Be("PO-2026-000002", "same counter, wider padding — values stay monotonic, codes stay unique");
    }

    [Fact]
    public async Task Case4_yearless_codes_use_the_year_zero_bucket_so_years_cannot_collide()
    {
        var (c, svc) = New();
        await Set(svc, "PO", year: false);
        (await c.Codes.NextAsync(RecordType.PurchaseOrder)).Should().Be("PO-0001");
        c.Clock.UtcNow = c.Clock.UtcNow.AddYears(1);
        (await c.Codes.NextAsync(RecordType.PurchaseOrder)).Should().Be("PO-0002",
            "one continuous counter across years — a year-less PO-0001 can exist only once, ever");
    }

    [Fact]
    public async Task Update_validates_prefix_shape_and_digit_range_and_previews_without_consuming()
    {
        var (c, svc) = New();
        await FluentActions.Awaiting(() => Set(svc, "p o")).Should().ThrowAsync<FormValidationException>();
        await FluentActions.Awaiting(() => Set(svc, "-PO")).Should().ThrowAsync<FormValidationException>();
        await FluentActions.Awaiting(() => Set(svc, "PO", digits: 2)).Should().ThrowAsync<FormValidationException>();
        await FluentActions.Awaiting(() => Set(svc, "PO", digits: 7)).Should().ThrowAsync<FormValidationException>();

        var dto = await Set(svc, "SPO", digits: 5);
        dto.NextPreview.Should().Be("SPO-2026-00001");
        (await svc.ListAsync()).Should().HaveCount(8);   // CF-FIX4-T2: +Grn (scheme-driven, was the string-prefix mint)
        (await c.Codes.NextAsync(RecordType.PurchaseOrder)).Should().Be("SPO-2026-00001", "the preview consumed nothing");
    }
}
