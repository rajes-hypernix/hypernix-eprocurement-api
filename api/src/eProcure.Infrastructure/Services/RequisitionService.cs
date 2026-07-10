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
    AppDbContext db, IClock clock, ICodeGenerator codes, IAuditLog audit) : IRequisitionService
{
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
        var now = clock.UtcNow;
        var pr = new PurchaseRequisition
        {
            Code = await codes.NextAsync("PR", ct),
            Requestor = req.Requestor, Memo = req.Memo,
            Department = req.Department, DepartmentCode = SourcingMapping.DimCode(req.Department),
            Location = req.Location, LocationCode = SourcingMapping.DimCode(req.Location),
            Category = req.Category, CategoryCode = SourcingMapping.DimCode(req.Category),
            Job = req.Job, JobCode = SourcingMapping.DimCode(req.Job),
            Currency = "MYR",
            Submitted = submit,
            Status = submit ? "Submitted" : "Draft",
            RaisedOn = DateOnly.FromDateTime(now),
            RaisedDate = now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            RequiredOn = ParseDate(req.RequiredDate),
            RequiredDate = DisplayDate(req.RequiredDate),
            Lines = [.. req.Lines.Select(l => PrLine.Create(l.ItemCode, l.Description, l.Qty, l.Uom, l.EstUnitPrice))],
            CreatedUtc = now, UpdatedUtc = now,
        };
        pr.RecomputeHeaderStatus();
        db.PurchaseRequisitions.Add(pr);
        await db.SaveChangesAsync(ct);
        await audit.WriteTransitionAsync("PurchaseRequisition", pr.Code,
            submit ? "PR submitted" : "PR draft created", fromState: null, toState: pr.HeaderStatus.ToString(), ct: ct);
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
        pr.RequiredOn = ParseDate(req.RequiredDate);
        pr.RequiredDate = DisplayDate(req.RequiredDate);

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
        return SourcingMapping.ToDto(pr, await NoQuoteLineIdsAsync(pr.Lines.Select(l => l.Id), ct));
    }

    public async Task<RequisitionDto> SubmitAsync(Guid id, CancellationToken ct = default)
    {
        var pr = await Load(id, ct);
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
        pr.Status = "Cancelled";
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

    private static DateOnly? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        foreach (var fmt in new[] { "yyyy-MM-dd", "dd/MM/yyyy" })
            if (DateOnly.TryParseExact(s, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d;
        return null;
    }

    private static string DisplayDate(string? s)
    {
        var d = ParseDate(s);
        return d?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? (s ?? "");
    }
}
