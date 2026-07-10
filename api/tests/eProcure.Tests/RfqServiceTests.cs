using eProcure.Application.Sourcing;
using eProcure.Domain;
using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Tests;

public sealed class RfqServiceTests
{
    private static RfqService NewService(out TestContext c)
    {
        c = TestContext.New();
        return new RfqService(c.Db, c.Clock, c.Codes, c.Audit, c.User, new eProcure.Infrastructure.Services.CustomListService(c.Db, c.Clock), Microsoft.Extensions.Options.Options.Create(new eProcure.Application.Sourcing.RfqGovernanceOptions()), Microsoft.Extensions.Logging.Abstractions.NullLogger<eProcure.Infrastructure.Services.RfqService>.Instance);
    }

    private static UpdateRfqDraftRequest DraftWith(
        IReadOnlyList<FormItemDto> form,
        IReadOnlyList<string>? invited = null,
        DateTime? closes = null) => new(
        Title: "Pump & VFD Package",
        Envelope: "Dual",
        Currency: "MYR",
        OpensUtc: null,
        ClosesUtc: closes,
        Lines: [new RfqLineDto("MEP-PUMP-075", "Centrifugal Pump", 4, "Unit", "PR-2026-0412")],
        FormItems: form,
        TechnicalSections: ["Company & experience"],
        CommercialSections: ["Commercial terms"],
        InvitedVendorIds: invited ?? [],
        TechnicalEvaluatorIds: ["u_hafiz"],
        CommercialEvaluatorIds: ["u_tan"]);

    [Fact]
    public async Task CreateDraft_GeneratesRfqCode_AndAudits()
    {
        var svc = NewService(out var c);
        var rfq = await svc.CreateDraftAsync(new CreateRfqDraftRequest("Pump pkg", ["PR-2026-0412"],
            [new RfqLineDto("MEP-PUMP-075", "Pump", 4, "Unit", "PR-2026-0412")]));

        rfq.Code.Should().StartWith("RFQ-2026-");
        rfq.Status.Should().Be("Draft");
        rfq.Lines.Should().HaveCount(1);
        (await c.Db.AuditEntries.AnyAsync(a => a.EntityType == "Rfq" && a.Action == "RFQ draft created"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task UpdateDraft_PersistsAllTwelveFieldTypes_WithGroupsAndSections()
    {
        var svc = NewService(out _);
        var created = await svc.CreateDraftAsync(new CreateRfqDraftRequest("x", [], []));

        var allTypes = FormItemVocab.Types; // 12 question field types
        var items = allTypes
            .Select((t, i) => new FormItemDto(
                Kind: "question",
                Group: i % 2 == 0 ? "technical" : "commercial",
                Section: i % 2 == 0 ? "Company & experience" : "Commercial terms",
                Label: $"Question {t}",
                Type: t,
                Required: i % 3 == 0,
                Config: t == "list" || t == "multi" ? "{\"options\":[\"A\",\"B\"]}" : "{}",
                Help: "",
                Order: i))
            .ToList();

        var updated = await svc.UpdateDraftAsync(created.Id, DraftWith(items));

        updated.FormItems.Should().HaveCount(12);
        updated.FormItems.Select(f => f.Type).Should().BeEquivalentTo(allTypes);
        updated.FormItems.Should().Contain(f => f.Group == "technical");
        updated.FormItems.Should().Contain(f => f.Group == "commercial");
    }

    [Fact]
    public async Task UpdateDraft_RejectsInvalidQuestionType()
    {
        var svc = NewService(out _);
        var created = await svc.CreateDraftAsync(new CreateRfqDraftRequest("x", [], []));
        var bad = new List<FormItemDto> { new("question", "technical", "", "Bad", "rainbow", false, "{}", "", 0) };

        var act = () => svc.UpdateDraftAsync(created.Id, DraftWith(bad));
        await act.Should().ThrowAsync<DomainRuleException>().WithMessage("*rainbow*");
    }

    [Fact]
    public async Task Release_CapturesClosesUtc_OpensRfq_AndAudits()
    {
        var svc = NewService(out var c);
        var created = await svc.CreateDraftAsync(new CreateRfqDraftRequest("x", [], []));
        var closes = c.Clock.UtcNow.AddDays(5);
        await svc.UpdateDraftAsync(created.Id, DraftWith([], invited: [Guid.NewGuid().ToString()], closes: closes));

        var released = await svc.ReleaseAsync(created.Id);

        released.Status.Should().Be("Open");
        released.ClosesUtc.Should().Be(closes);
        released.OpensUtc.Should().NotBeNull();
        (await c.Db.AuditEntries.AnyAsync(a => a.EntityType == "Rfq" && a.Action == "RFQ released"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Release_WithoutVendors_IsRejected()
    {
        var svc = NewService(out var c);
        var created = await svc.CreateDraftAsync(new CreateRfqDraftRequest("x", [], []));
        await svc.UpdateDraftAsync(created.Id, DraftWith([], invited: [], closes: c.Clock.UtcNow.AddDays(5)));

        var act = () => svc.ReleaseAsync(created.Id);
        await act.Should().ThrowAsync<DomainRuleException>().WithMessage("*at least one vendor*");
    }

    [Fact]
    public async Task Release_WithoutClosingDate_IsRejected()
    {
        var svc = NewService(out _);
        var created = await svc.CreateDraftAsync(new CreateRfqDraftRequest("x", [], []));
        await svc.UpdateDraftAsync(created.Id, DraftWith([], invited: [Guid.NewGuid().ToString()], closes: null));

        var act = () => svc.ReleaseAsync(created.Id);
        await act.Should().ThrowAsync<DomainRuleException>().WithMessage("*closing date*");
    }

    [Fact]
    public async Task Release_Twice_IsRejected()
    {
        var svc = NewService(out var c);
        var created = await svc.CreateDraftAsync(new CreateRfqDraftRequest("x", [], []));
        await svc.UpdateDraftAsync(created.Id, DraftWith([], invited: [Guid.NewGuid().ToString()], closes: c.Clock.UtcNow.AddDays(5)));
        await svc.ReleaseAsync(created.Id);

        var act = () => svc.ReleaseAsync(created.Id);
        await act.Should().ThrowAsync<DomainRuleException>().WithMessage("*already released*");
    }
}
