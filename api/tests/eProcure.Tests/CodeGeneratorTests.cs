using eProcure.Infrastructure.Services;
using FluentAssertions;

namespace eProcure.Tests;

public sealed class CodeGeneratorTests
{
    [Fact]
    public async Task NextAsync_FirstCode_IsPrefixYearAndPaddedOne()
    {
        using var db = TestDb.NewContext();
        var clock = new FakeClock(new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc));
        var gen = new CodeGenerator(db, clock);

        var code = await gen.NextAsync("RFQ");

        code.Should().Be("RFQ-2026-0001");
    }

    [Fact]
    public async Task NextAsync_CalledRepeatedly_IncrementsMonotonicallyPerPrefix()
    {
        using var db = TestDb.NewContext();
        var clock = new FakeClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var gen = new CodeGenerator(db, clock);

        var first = await gen.NextAsync("PO");
        var second = await gen.NextAsync("PO");
        var third = await gen.NextAsync("PO");

        first.Should().Be("PO-2026-0001");
        second.Should().Be("PO-2026-0002");
        third.Should().Be("PO-2026-0003");
    }

    [Fact]
    public async Task NextAsync_DifferentPrefixes_HaveIndependentSequences()
    {
        using var db = TestDb.NewContext();
        var clock = new FakeClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var gen = new CodeGenerator(db, clock);

        var rfq1 = await gen.NextAsync("RFQ");
        var po1 = await gen.NextAsync("PO");
        var rfq2 = await gen.NextAsync("RFQ");

        rfq1.Should().Be("RFQ-2026-0001");
        po1.Should().Be("PO-2026-0001");
        rfq2.Should().Be("RFQ-2026-0002");
    }

    [Fact]
    public async Task NextAsync_NewYear_RestartsSequence()
    {
        using var db = TestDb.NewContext();
        var clock = new FakeClock(new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        var gen = new CodeGenerator(db, clock);

        var last2026 = await gen.NextAsync("RFQ");
        clock.UtcNow = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var first2027 = await gen.NextAsync("RFQ");

        last2026.Should().Be("RFQ-2026-0001");
        first2027.Should().Be("RFQ-2027-0001");
    }

    [Fact]
    public async Task NextAsync_LowercasePrefix_IsNormalisedToUpper()
    {
        using var db = TestDb.NewContext();
        var clock = new FakeClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var gen = new CodeGenerator(db, clock);

        var code = await gen.NextAsync("rfq");

        code.Should().Be("RFQ-2026-0001");
    }
}
