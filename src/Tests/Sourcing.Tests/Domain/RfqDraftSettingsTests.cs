using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Sourcing.Domain;
using FSH.Modules.Sourcing.Features.v1.Rfqs;
using Shouldly;
using Xunit;

namespace Sourcing.Tests.Domain;

public sealed class RfqDraftSettingsTests
{
    [Fact]
    public void UpdateDraft_Should_Persist_Settings_And_Incoterm()
    {
        var rfq = BlankDraft();
        var incotermId = Guid.CreateVersion7();
        var opens = DateTime.UtcNow.AddDays(1);
        var closes = DateTime.UtcNow.AddDays(10);
        var clarify = DateTime.UtcNow.AddDays(5);

        rfq.UpdateDraft(
            "Pump RFQ",
            RfqEnvelope.Dual,
            "MYR",
            opens,
            closes,
            [new RfqLine("L1", "PMP", "Pump", 2m, "EA", "PR-1", null)],
            [new FormItem("question", "Technical", "HSE", "ISO cert?", "yesno", true, null, null, 0)],
            ["HSE"],
            ["Pricing"],
            ["tech-user-1"],
            ["comm-user-1"],
            clarify,
            bidValidityDays: 30,
            partialBidsAllowed: false,
            incotermId,
            "FOB",
            "Port Klang");

        rfq.Title.ShouldBe("Pump RFQ");
        rfq.ClarificationDeadlineUtc.ShouldBe(clarify);
        rfq.BidValidityDays.ShouldBe(30);
        rfq.PartialBidsAllowed.ShouldBeFalse();
        rfq.IncotermId.ShouldBe(incotermId);
        rfq.IncotermCode.ShouldBe("FOB");
        rfq.IncotermSuffix.ShouldBe("Port Klang");
        rfq.TechnicalSections.ShouldBe(["HSE"]);
        rfq.CommercialSections.ShouldBe(["Pricing"]);
        rfq.TechnicalEvaluatorIds.ShouldBe(["tech-user-1"]);
        rfq.CommercialEvaluatorIds.ShouldBe(["comm-user-1"]);
        rfq.FormItems.Count.ShouldBe(1);
        rfq.FormItems[0].Section.ShouldBe("HSE");
        rfq.FormItems[0].Type.ShouldBe("yesno");
    }

    [Fact]
    public void UpdateDraft_Should_Clear_Incoterm_When_Null()
    {
        var rfq = BlankDraft();
        var id = Guid.CreateVersion7();
        rfq.UpdateDraft(
            "T", RfqEnvelope.Single, "MYR", null, DateTime.UtcNow.AddDays(3),
            [], [], [], [], [], [],
            incotermId: id, incotermCode: "CIF", incotermSuffix: "Penang");

        rfq.UpdateDraft(
            "T", RfqEnvelope.Single, "MYR", null, DateTime.UtcNow.AddDays(3),
            [], [], [], [], [], [],
            incotermId: null, incotermCode: null, incotermSuffix: null);

        rfq.IncotermId.ShouldBeNull();
        rfq.IncotermCode.ShouldBeNull();
        rfq.IncotermSuffix.ShouldBeNull();
    }

    [Fact]
    public void UpdateDraft_Should_Normalize_IncotermCode()
    {
        var rfq = BlankDraft();
        rfq.UpdateDraft(
            "T", RfqEnvelope.Single, "MYR", null, DateTime.UtcNow.AddDays(3),
            [], [], [], [], [], [],
            incotermId: Guid.CreateVersion7(), incotermCode: "  fob  ", incotermSuffix: "  Klang  ");

        rfq.IncotermCode.ShouldBe("FOB");
        rfq.IncotermSuffix.ShouldBe("Klang");
    }

    private static Rfq BlankDraft() =>
        Rfq.CreateDraft("RFQ-T", "T", RfqEnvelope.Dual, "MYR", "owner", [], []);
}

public sealed class RfqIncotermSupportTests
{
    private static readonly Guid ActiveId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid InactiveId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static IReadOnlyList<IncotermDto> Catalog() =>
    [
        new(ActiveId, "FOB", "Free On Board", true, DateTimeOffset.UtcNow),
        new(InactiveId, "EXW", "Ex Works", false, DateTimeOffset.UtcNow),
    ];

    [Fact]
    public void Resolve_Should_Return_Null_When_Empty()
    {
        RfqIncotermSupport.Resolve(null, null, Catalog()).ShouldBe((null, null));
        RfqIncotermSupport.Resolve(null, "  ", Catalog()).ShouldBe((null, null));
    }

    [Fact]
    public void Resolve_Should_Snapshot_Code_From_Active_Id()
    {
        var result = RfqIncotermSupport.Resolve(ActiveId, null, Catalog());
        result.ShouldBe((ActiveId, "FOB"));
    }

    [Fact]
    public void Resolve_Should_Reject_Inactive_Id()
    {
        Should.Throw<SourcingRuleException>(() => RfqIncotermSupport.Resolve(InactiveId, null, Catalog()))
            .Message.ShouldContain("inactive");
    }

    [Fact]
    public void Resolve_Should_Reject_Unknown_Id()
    {
        Should.Throw<SourcingRuleException>(() => RfqIncotermSupport.Resolve(Guid.NewGuid(), null, Catalog()))
            .Message.ShouldContain("was not found");
    }

    [Fact]
    public void Resolve_Should_Map_Legacy_Code_To_Active_Id()
    {
        var result = RfqIncotermSupport.Resolve(null, "fob", Catalog());
        result.ShouldBe((ActiveId, "FOB"));
    }

    [Fact]
    public void Resolve_Should_Reject_Inactive_Or_Unknown_Code()
    {
        Should.Throw<SourcingRuleException>(() => RfqIncotermSupport.Resolve(null, "EXW", Catalog()))
            .Message.ShouldContain("not an active master term");
        Should.Throw<SourcingRuleException>(() => RfqIncotermSupport.Resolve(null, "ZZZ", Catalog()))
            .Message.ShouldContain("not an active master term");
    }
}
