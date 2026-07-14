using System.Globalization;
using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Sourcing;
using eProcure.Domain;
using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// PR list/read + portal CRUD + line lifecycle (PR-MODULE-SPEC §3/§4). Every transition goes
/// through the rich <see cref="PrLine"/>/<see cref="PurchaseRequisition"/> domain methods and
/// writes a typed <see cref="AuditEntry"/>. Additive: <see cref="ListAsync"/> and the existing
/// Confirm-lines read path are unchanged.
/// </summary>
public sealed class RequisitionService(
    AppDbContext db, IClock clock, ICodeGenerator codes, IAuditLog audit,
    Application.Segments.ISegmentProjection segments,
    Application.Forms.IEntryFormSubmitGuard entryForm) : IRequisitionService
{
    /// <summary>D7 (OD-D7-2/3): the caller's RESOLVED form's requiredOnForm gates SUBMIT
    /// only, never draft save. Native keys check the incoming values; cf_/seg_ keys check
    /// their value/assignment rows (which need the record id — a create-with-submit whose
    /// form requires them fails loudly with "save a draft first", the honest one-shot answer).</summary>
    private Task GuardSubmitAsync(SavePrRequest req, Guid recordId, CancellationToken ct) =>
        entryForm.EnsureSubmittableAsync(Domain.Views.RecordType.Requisition, recordId,
            new Dictionary<string, string?>
            {
                ["Requestor"] = req.Requestor, ["Department"] = req.Department,
                ["Location"] = req.Location, ["Category"] = req.Category, ["Job"] = req.Job,
                ["Memo"] = req.Memo, ["RequiredDate"] = req.RequiredDate?.ToString("yyyy-MM-dd"),
            }, ct);

    // HARDENING: PR create/edit + line transitions are RowVersion concurrency-token surfaces (§2.6).

    public async Task<IReadOnlyList<RequisitionDto>> ListAsync(CancellationToken ct = default)
    {
        var prs = await db.PurchaseRequisitions.AsNoTracking().OrderBy(p => p.Code).ToListAsync(ct);
        var noQuotes = await NoQuoteLineIdsAsync(prs.SelectMany(p => p.Lines.Select(l => l.Id)), ct);
        return prs.Select(p => SourcingMapping.ToDto(p, noQuotes)).ToList();
    }

    public async Task<RequisitionDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var pr = await db.PurchaseRequisitions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pr is null) return null;
        var noQuotes = await NoQuoteLineIdsAsync(pr.Lines.Select(l => l.Id), ct);
        return SourcingMapping.ToDto(pr, noQuotes);
    }

    public async Task<RequisitionDto> CreateAsync(SavePrRequest req, bool submit, CancellationToken ct = default)
    {
        // Guard BEFORE the code mint: a rejected submit must not burn a gap-free number.
        if (submit) await GuardSubmitAsync(req, Guid.Empty, ct);
        var now = clock.UtcNow;
        var pr = new PurchaseRequisition
        {
            Code = await codes.NextAsync(Domain.Views.RecordType.Requisition, ct),
            Requestor = req.Requestor, Memo = req.Memo,
            Department = req.Department, DepartmentCode = SourcingMapping.DimCode(req.Department),
            Location = req.Location, LocationCode = SourcingMapping.DimCode(req.Location),
            Category = req.Category, CategoryCode = SourcingMapping.DimCode(req.Category),
            Job = req.Job, JobCode = SourcingMapping.DimCode(req.Job),
            Currency = "MYR",
            Submitted = submit,
            EntryFormId = req.EntryFormId,   // CFF-T1: persist the form the PR is entered on
            RaisedOn = DateOnly.FromDateTime(now),
            RequiredOn = req.RequiredDate,
            Lines = [.. req.Lines.Select(l => PrLine.Create(l.ItemCode, l.Description, l.Qty, l.Uom, l.EstUnitPrice))],
            CreatedUtc = now, UpdatedUtc = now,
        };
        pr.RecomputeHeaderStatus();
        pr.SyncLegacyStatus();          // "Submitted"/"Draft" == HeaderStatus at creation
        db.PurchaseRequisitions.Add(pr);
        await db.SaveChangesAsync(ct);
        await audit.WriteTransitionAsync("PurchaseRequisition", pr.Code,
            submit ? "PR submitted" : "PR draft created", fromState: null, toState: pr.HeaderStatus.ToString(), ct: ct);
        // D6 (iii-a): project the dimension columns (the single truth) into the system
        // segments' assignments. Both write paths carry this call — the Postgres probe
        // (SegmentProjectionProbeTests) goes red if a future path forgets it.
        await segments.ProjectRequisitionAsync(pr.Id, ct);
        return SourcingMapping.ToDto(pr);
    }

    public async Task<RequisitionDto> UpdateAsync(Guid id, SavePrRequest req, CancellationToken ct = default)
    {
        var pr = await Load(id, ct);
        if (pr.HeaderStatus == PrHeaderStatus.Cancelled)
            throw new DomainRuleException("A cancelled PR cannot be edited.");

        pr.Requestor = req.Requestor; pr.Memo = req.Memo;
        pr.Department = req.Department; pr.DepartmentCode = SourcingMapping.DimCode(req.Department);
        pr.Location = req.Location; pr.LocationCode = SourcingMapping.DimCode(req.Location);
        pr.Category = req.Category; pr.CategoryCode = SourcingMapping.DimCode(req.Category);
        pr.Job = req.Job; pr.JobCode = SourcingMapping.DimCode(req.Job);
        pr.RequiredOn = req.RequiredDate;
        pr.EntryFormId = req.EntryFormId;   // CFF-T1: keep the chosen form current on edit

        // Open lines absent from the request are removed; locked (InRfq/Awarded/Cancelled) lines
        // are always kept and read-only.
        foreach (var gone in pr.Lines
            .Where(l => l.LifecycleStatus == PrLineStatus.Open && req.Lines.All(i => i.Id != l.Id)).ToList())
            pr.Lines.Remove(gone);

        // Edit Open lines in place; locked-line edits are ignored.
        foreach (var line in pr.Lines.Where(l => l.LifecycleStatus == PrLineStatus.Open))
        {
            var input = req.Lines.FirstOrDefault(i => i.Id == line.Id);
            if (input is null) continue;
            line.ItemCode = input.ItemCode; line.Description = input.Description;
            line.Qty = input.Qty; line.Uom = input.Uom; line.EstUnitPrice = input.EstUnitPrice;
        }

        // New (id-less) lines are added as Open. The explicit Added state is required so EF's
        // owned-collection tracking inserts the child instead of mis-flagging its siblings.
        foreach (var input in req.Lines.Where(i => i.Id is null))
        {
            var line = PrLine.Create(input.ItemCode, input.Description, input.Qty, input.Uom, input.EstUnitPrice);
            pr.Lines.Add(line);
            db.Entry(line).State = EntityState.Added;
        }

        pr.RecomputeHeaderStatus();
        pr.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("PurchaseRequisition", pr.Code, "PR edited", after: pr.HeaderStatus.ToString(), ct: ct);
        // D6 (iii-a): the second write path's projection call — see CreateAsync.
        await segments.ProjectRequisitionAsync(pr.Id, ct);
        return SourcingMapping.ToDto(pr, await NoQuoteLineIdsAsync(pr.Lines.Select(l => l.Id), ct));
    }

    public async Task<RequisitionDto> SubmitAsync(Guid id, CancellationToken ct = default)
    {
        var pr = await Load(id, ct);
        await entryForm.EnsureSubmittableAsync(Domain.Views.RecordType.Requisition, pr.Id,
            new Dictionary<string, string?>
            {
                ["Requestor"] = pr.Requestor, ["Department"] = pr.Department,
                ["Location"] = pr.Location, ["Category"] = pr.Category, ["Job"] = pr.Job,
                ["Memo"] = pr.Memo, ["RequiredDate"] = pr.RequiredOn?.ToString("yyyy-MM-dd"),
            }, ct);
        var from = pr.HeaderStatus.ToString();
        pr.Submit(clock.UtcNow);           // Draft → Submitted, guards ≥1 open line
        await db.SaveChangesAsync(ct);
        await audit.WriteTransitionAsync("PurchaseRequisition", pr.Code, "PR submitted",
            fromState: from, toState: pr.HeaderStatus.ToString(), ct: ct);
        return SourcingMapping.ToDto(pr, await NoQuoteLineIdsAsync(pr.Lines.Select(l => l.Id), ct));
    }

    public Task<RequisitionDto> CancelLineAsync(Guid id, Guid lineId, string? reason, CancellationToken ct = default)
        => LineTransitionAsync(id, lineId, (l, now) => l.Cancel(reason ?? "", now), ct);

    public Task<RequisitionDto> ReleaseLineAsync(Guid id, Guid lineId, string? reason, CancellationToken ct = default)
        => LineTransitionAsync(id, lineId, (l, now) => { var t = l.ReleaseForResourcing(reason ?? "", now); l.Ref = null; return t; }, ct);

    public Task<RequisitionDto> ReopenLineAsync(Guid id, Guid lineId, CancellationToken ct = default)
        => LineTransitionAsync(id, lineId, (l, now) => l.Reopen(now), ct);

    public Task<RequisitionDto> ReserveLineAsync(Guid id, Guid lineId, CancellationToken ct = default)
        => LineTransitionAsync(id, lineId, (l, now) => l.AddToDraft(now), ct);

    public Task<RequisitionDto> UnreserveLineAsync(Guid id, Guid lineId, CancellationToken ct = default)
        => LineTransitionAsync(id, lineId, (l, now) => l.AbandonDraft(now), ct);

    public async Task<RequisitionDto> CancelPrAsync(Guid id, string? reason, CancellationToken ct = default)
    {
        var pr = await Load(id, ct);
        // [C7] A PR cannot be cancelled while any line is live in an RFQ or awarded.
        if (pr.Lines.Any(l => l.LifecycleStatus is PrLineStatus.InRfq or PrLineStatus.Awarded))
            throw new DomainRuleException("Cannot cancel a PR while a line is in an RFQ or awarded.");

        var now = clock.UtcNow;
        foreach (var line in pr.Lines.Where(l => l.LifecycleStatus == PrLineStatus.Open))
            line.Cancel(reason ?? "", now);
        pr.RecomputeHeaderStatus();
        pr.Cancel();                    // keeps the legacy display status in step
        pr.UpdatedUtc = now;
        await db.SaveChangesAsync(ct);
        await audit.WriteTransitionAsync("PurchaseRequisition", pr.Code, "PR cancelled",
            fromState: null, toState: pr.HeaderStatus.ToString(), reason: reason, ct: ct);
        return SourcingMapping.ToDto(pr);
    }

    // ---- helpers ----

    private async Task<RequisitionDto> LineTransitionAsync(
        Guid id, Guid lineId, Func<PrLine, DateTime, PrLineTransition> transition, CancellationToken ct)
    {
        var pr = await Load(id, ct);
        var line = pr.Lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new NotFoundException($"Line {lineId} not found on {pr.Code}.");

        var t = transition(line, clock.UtcNow);   // throws DomainRuleException on an illegal transition
        pr.RecomputeHeaderStatus();
        pr.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteTransitionAsync("PrLine", line.Id.ToString(), t.Action,
            t.From.ToString(), t.To.ToString(), t.Reason, ct);
        return SourcingMapping.ToDto(pr, await NoQuoteLineIdsAsync(pr.Lines.Select(l => l.Id), ct));
    }

    private async Task<PurchaseRequisition> Load(Guid id, CancellationToken ct) =>
        await db.PurchaseRequisitions.FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new NotFoundException($"Requisition {id} not found.");

    /// <summary>Lines that are Open with their most-recent sourcing link Returned ⇒ "No quotes".</summary>
    private async Task<IReadOnlySet<Guid>> NoQuoteLineIdsAsync(IEnumerable<Guid> lineIds, CancellationToken ct)
    {
        var ids = lineIds.ToHashSet();
        if (ids.Count == 0) return new HashSet<Guid>();
        var links = await db.PrLineSourcings.AsNoTracking().Where(s => ids.Contains(s.PrLineId)).ToListAsync(ct);
        return links
            .GroupBy(s => s.PrLineId)
            .Where(g => g.OrderByDescending(s => s.CreatedUtc).First().LinkStatus == LinkStatus.Returned)
            .Select(g => g.Key)
            .ToHashSet();
    }

}
