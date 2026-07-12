using eProcure.Application.Sourcing;
using eProcure.Domain;
using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice B — PR CRUD + line lifecycle service (EDGE-CASES C1–C9, B6). Existing ListAsync /
/// Confirm-lines read path is covered elsewhere and untouched.
/// </summary>
public class RequisitionServiceTests
{
    private static RequisitionService NewService(out TestContext c)
    {
        c = TestContext.New();
        return new RequisitionService(c.Db, c.Clock, c.Codes, c.Audit, new SegmentProjection(c.Db, c.Clock));
    }

    private static SavePrRequest Save(params PrLineInput[] lines) => new(
        Requestor: "Aishah Karim", Department: "Maintenance", Location: "Bintulu Plant",
        Category: "Piping", Job: "JOB-1", Memo: "Test PR", RequiredDate: new DateOnly(2026, 8, 15),
        Lines: lines.Length == 0 ? [new PrLineInput(null, "ITEM-1", "Widget", 10, "Unit", 100m)] : lines);

    /// <summary>Persists a PR with explicit line states (bypassing create) for lifecycle tests.</summary>
    private static async Task<PurchaseRequisition> SeedPr(TestContext c, params PrLineStatus[] lineStates)
    {
        var pr = new PurchaseRequisition
        {
            Code = "PR-2026-0500", Requestor = "Seed", Department = "Ops", Submitted = true,   // Status defaults to "Approved"
            Lines = [.. lineStates.Select((s, i) => PrLine.Create($"ITEM-{i}", $"Item {i}", 5, "Unit", 20m, s))],
            CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow,
        };
        pr.RecomputeHeaderStatus();
        c.Db.PurchaseRequisitions.Add(pr);
        await c.Db.SaveChangesAsync();
        c.Db.ChangeTracker.Clear();
        return pr;
    }

    [Fact] // C1
    public async Task Create_SaveDraft_IsDraft_WithSequenceCode_AndAudits()
    {
        var svc = NewService(out var c);
        var pr = await svc.CreateAsync(Save(), submit: false);

        pr.HeaderStatus.Should().Be("Draft");
        pr.Submitted.Should().BeFalse();
        pr.Code.Should().StartWith("PR-").And.EndWith("-0001");
        pr.Lines.Should().OnlyContain(l => l.LifecycleStatus == "Open");
        (await c.Db.AuditEntries.CountAsync()).Should().Be(1);
    }

    [Fact] // Bug 3 — a saved Draft can be submitted, with a typed audit
    public async Task Submit_DraftPr_FlipsToSubmitted_AndAudits()
    {
        var svc = NewService(out var c);
        var draft = await svc.CreateAsync(Save(), submit: false);
        draft.HeaderStatus.Should().Be("Draft");

        var result = await svc.SubmitAsync(draft.Id);

        result.HeaderStatus.Should().Be("Submitted");
        result.Submitted.Should().BeTrue();
        var audit = await c.Db.AuditEntries.FirstAsync(a => a.Action == "PR submitted");
        audit.FromState.Should().Be("Draft");
        audit.ToState.Should().Be("Submitted");
        // T5: the transition stamps SubmittedUtc inside the domain method (via IClock).
        var pr = await c.Db.PurchaseRequisitions.FirstAsync(p => p.Id == draft.Id);
        pr.SubmittedUtc.Should().Be(c.Clock.UtcNow);
    }

    [Fact] // C2
    public async Task Create_Submit_IsSubmitted_LinesOpen()
    {
        var svc = NewService(out _);
        var pr = await svc.CreateAsync(Save(), submit: true);
        pr.HeaderStatus.Should().Be("Submitted");
        pr.Submitted.Should().BeTrue();
        pr.Lines.Should().OnlyContain(l => l.LifecycleStatus == "Open");
    }

    [Fact] // C3 + C4
    public async Task Update_EditsHeaderAndOpenLine()
    {
        var svc = NewService(out _);
        var created = await svc.CreateAsync(Save(), submit: true);
        var lineId = created.Lines[0].Id;

        var edited = await svc.UpdateAsync(created.Id, Save(new PrLineInput(lineId, "ITEM-1", "Bigger widget", 25, "Box", 150m)) with { Requestor = "Lim Chee Kong" });

        edited.Requestor.Should().Be("Lim Chee Kong");
        edited.Lines[0].Qty.Should().Be(25);
        edited.Lines[0].Description.Should().Be("Bigger widget");
    }

    [Fact] // C5 — InRfq line is read-only; edits are ignored
    public async Task Update_LockedLine_IgnoresEdits()
    {
        var svc = NewService(out var c);
        var pr = await SeedPr(c, PrLineStatus.InRfq);
        var lineId = pr.Lines[0].Id;

        var result = await svc.UpdateAsync(pr.Id, Save(new PrLineInput(lineId, "HACKED", "hacked", 999, "X", 9m)));

        var line = result.Lines.Single();
        line.LifecycleStatus.Should().Be("InRfq");
        line.ItemCode.Should().Be("ITEM-0");      // unchanged
        line.Qty.Should().Be(5);
    }

    [Fact] // C6 — add a new line to a partially-sourced PR
    public async Task Update_AddsNewOpenLine_ToPartiallySourcedPr()
    {
        var svc = NewService(out var c);
        var pr = await SeedPr(c, PrLineStatus.InRfq, PrLineStatus.Open);
        var inputs = pr.Lines.Select(l => new PrLineInput(l.Id, l.ItemCode, l.Description, l.Qty, l.Uom, l.EstUnitPrice))
            .Append(new PrLineInput(null, "NEW-1", "Fresh demand", 3, "Unit", 12m)).ToArray();

        var result = await svc.UpdateAsync(pr.Id, Save(inputs));

        result.Lines.Should().HaveCount(3);
        result.Lines.Should().ContainSingle(l => l.ItemCode == "NEW-1" && l.LifecycleStatus == "Open");
    }

    [Fact] // C7 — cannot cancel a PR while a line is InRfq/Awarded
    public async Task CancelPr_WithLiveLine_IsRejected()
    {
        var svc = NewService(out var c);
        var pr = await SeedPr(c, PrLineStatus.InRfq, PrLineStatus.Open);
        var act = () => svc.CancelPrAsync(pr.Id, "no longer needed");
        await act.Should().ThrowAsync<DomainRuleException>();
    }

    [Fact] // C8 — cancel a PR whose lines are all Open/Cancelled
    public async Task CancelPr_AllOpenOrCancelled_CancelsOpenLines_AndHeader()
    {
        var svc = NewService(out var c);
        var pr = await SeedPr(c, PrLineStatus.Open, PrLineStatus.Cancelled);

        var result = await svc.CancelPrAsync(pr.Id, "duplicate");

        result.HeaderStatus.Should().Be("Cancelled");
        result.Lines.Should().OnlyContain(l => l.LifecycleStatus == "Cancelled");
    }

    [Fact] // C9 — a seeded PR opens and edits identically to a portal-created one
    public async Task SeededPr_GetAndUpdate_BehaveLikePortalPr()
    {
        var svc = NewService(out var c);
        var pr = await SeedPr(c, PrLineStatus.Open);

        var got = await svc.GetAsync(pr.Id);
        got.Should().NotBeNull();

        var edited = await svc.UpdateAsync(pr.Id, Save(new PrLineInput(pr.Lines[0].Id, "ITEM-0", "Edited", 7, "Unit", 20m)) with { Memo = "Edited memo" });
        edited!.Memo.Should().Be("Edited memo");
        edited.Lines[0].Description.Should().Be("Edited");
    }

    [Fact] // C1 line-level: cancel an open line records reason + audit
    public async Task CancelLine_OpenLine_CancelsAndAudits()
    {
        var svc = NewService(out var c);
        var pr = await SeedPr(c, PrLineStatus.Open, PrLineStatus.Open);
        var lineId = pr.Lines[0].Id;

        var result = await svc.CancelLineAsync(pr.Id, lineId, "duplicate of PR-0423");

        result.Lines.Single(l => l.Id == lineId).LifecycleStatus.Should().Be("Cancelled");
        var audit = await c.Db.AuditEntries.FirstAsync(a => a.EntityType == "PrLine");
        audit.FromState.Should().Be("Open");
        audit.ToState.Should().Be("Cancelled");
        audit.Reason.Should().Be("duplicate of PR-0423");
    }

    [Fact] // A11 at service level — cancelling an InRfq line is rejected
    public async Task CancelLine_InRfqLine_IsRejected()
    {
        var svc = NewService(out var c);
        var pr = await SeedPr(c, PrLineStatus.InRfq);
        var act = () => svc.CancelLineAsync(pr.Id, pr.Lines[0].Id, "x");
        await act.Should().ThrowAsync<DomainRuleException>();
    }

    [Fact] // B6 — reopening a line in a Cancelled PR flips the header off Cancelled
    public async Task ReopenLine_InCancelledPr_FlipsHeaderOffCancelled()
    {
        var svc = NewService(out var c);
        var pr = await SeedPr(c, PrLineStatus.Cancelled, PrLineStatus.Cancelled);
        (await svc.GetAsync(pr.Id))!.HeaderStatus.Should().Be("Cancelled");

        var result = await svc.ReopenLineAsync(pr.Id, pr.Lines[0].Id);

        result.HeaderStatus.Should().NotBe("Cancelled");
        result.HeaderStatus.Should().Be("Submitted");
    }
}
