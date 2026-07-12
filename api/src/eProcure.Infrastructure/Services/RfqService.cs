using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Configuration;
using eProcure.Application.Sourcing;
using eProcure.Domain;
using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eProcure.Infrastructure.Services;

public sealed class RfqService(
    AppDbContext db,
    IClock clock,
    ICodeGenerator codes,
    IAuditLog audit,
    ICurrentUser user,
    ICustomListService customLists,
    IOptions<RfqGovernanceOptions> governance,
    ILogger<RfqService> logger) : IRfqService
{
    private readonly RfqGovernanceOptions _gov = governance.Value;
    public async Task<IReadOnlyList<RfqListItem>> ListAsync(CancellationToken ct = default)
    {
        var rfqs = await db.Rfqs.AsNoTracking().Include(r => r.Invitations)
            .OrderByDescending(r => r.CreatedUtc).ToListAsync(ct);

        // Vendor principals see ONLY the RFQs they hold a live invitation to — the endpoint must not
        // leak other tenders' titles/metadata (register "Known scoping gaps"). Buyers/admins see all.
        // Mirrors the DashboardService "myInvited" derivation (Rescinded rows excluded).
        if (user.VendorId is { } vid)
            rfqs = rfqs.Where(r => r.Invitations.Any(i => i.VendorId == vid && i.Status != RfqInvitationStatus.Rescinded)).ToList();

        var bidCounts = await db.Bids.AsNoTracking().Where(b => b.Submitted)
            .GroupBy(b => b.RfqId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return rfqs.Select(r => SourcingMapping.ToListItem(r, bidCounts.GetValueOrDefault(r.Id))).ToList();
    }

    public async Task<RfqDetail?> GetAsync(Guid id, CancellationToken ct = default)
    {
        // A vendor viewing an RFQ marks their invitation Viewed (T1, passive, first view only).
        // Fire-and-forget-safe: this best-effort side effect must never fail the read of the RFQ,
        // but a persistent failure must be visible in logs (not silently swallowed).
        try { await MarkViewedIfVendorAsync(id, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Mark-viewed side effect failed for RFQ {RfqId}; returning the RFQ regardless.", id); }

        var r = await db.Rfqs.AsNoTracking().Include(x => x.Invitations)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return null;

        // [G] Vendor resource scoping (Slice F patch, AUTHORIZATION-MATRIX Obs-1): a vendor may
        // read an RFQ's detail only while holding a live (non-Rescinded) invitation — the same
        // rule the list applies above. A foreign probe gets the vendor-scoping 403 convention.
        if (user.VendorId is { } vGuard &&
            !r.Invitations.Any(i => i.VendorId == vGuard && i.Status != RfqInvitationStatus.Rescinded))
            throw new ForbiddenException("Vendor users may only access RFQs they are invited to.");

        var events = await db.RfqEvents.AsNoTracking().Where(e => e.RfqId == id)
            .OrderByDescending(e => e.OccurredUtc).ToListAsync(ct);

        // Vendor names for both invitations and events, in one lookup.
        var vendorIds = r.Invitations.Select(i => i.VendorId)
            .Concat(events.Where(e => e.VendorId is not null).Select(e => e.VendorId!.Value)).Distinct().ToList();
        var vendors = await db.Vendors.AsNoTracking().Where(v => vendorIds.Contains(v.Id))
            .Select(v => new { v.Id, v.Name, v.Categories }).ToDictionaryAsync(v => v.Id, ct);
        string NameOf(Guid vid) => vendors.TryGetValue(vid, out var v) ? v.Name : vid.ToString();

        // Legacy per-invited-vendor summary (non-Rescinded), unchanged semantics for existing consumers.
        var submitted = (await db.Bids.AsNoTracking()
            .Where(b => b.RfqId == id && b.Submitted).Select(b => b.VendorId).ToListAsync(ct)).ToHashSet();
        var invited = r.Invitations
            .Where(i => i.Status != RfqInvitationStatus.Rescinded)
            .Select(i => new RfqInvitedVendorDto(
                i.VendorId.ToString(), NameOf(i.VendorId),
                vendors.TryGetValue(i.VendorId, out var v) ? v.Categories.FirstOrDefault() ?? "—" : "—",
                submitted.Contains(i.VendorId))).ToList();

        var invitations = r.Invitations
            .OrderBy(i => i.InvitedUtc)
            .Select(i => SourcingMapping.ToDto(i, NameOf(i.VendorId))).ToList();
        var eventDtos = events
            .Select(e => SourcingMapping.ToDto(e, e.VendorId is { } vid ? NameOf(vid) : null)).ToList();
        var extensionCount = events.Count(e => e.EventType == RfqEventType.Extended);

        return SourcingMapping.ToDetail(r, invited, invitations, eventDtos, extensionCount, _gov.MaxExtensions);
    }

    /// <summary>T1 side effect: a vendor's GET of an RFQ marks their own invitation Viewed once
    /// (RFQ-LIFECYCLE-ADDENDUM §6). No-op for buyers and for invitations already past Invited.</summary>
    private async Task MarkViewedIfVendorAsync(Guid rfqId, CancellationToken ct)
    {
        if (user.VendorId is not Guid vid) return;
        var rfq = await db.Rfqs.Include(r => r.Invitations).FirstOrDefaultAsync(r => r.Id == rfqId, ct);
        var inv = rfq?.Invitations.FirstOrDefault(i => i.VendorId == vid && i.RoundNumber == rfq.RoundNumber);
        if (rfq is null || inv is null || inv.Status != RfqInvitationStatus.Invited) return;
        rfq.MarkInvitationViewed(vid, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task<RfqDetail> CreateDraftAsync(CreateRfqDraftRequest req, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var rfq = new Rfq
        {
            Code = await codes.NextAsync(Domain.Views.RecordType.Rfq, ct),
            Title = req.Title ?? "",
            Envelope = RfqEnvelope.Dual,
            // Status defaults to Draft (the setter is now private).
            OwnerUserId = user.UserId,
            PrRefs = req.PrRefs.Distinct().ToList(),
            Lines = req.Lines.Select(SourcingMapping.ToEntity).ToList(),
            CreatedUtc = now,
            UpdatedUtc = now,
        };
        db.Rfqs.Add(rfq);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Rfq", rfq.Code, "RFQ draft created", after: rfq.Title, ct: ct);
        return SourcingMapping.ToDetail(rfq, maxExtensions: _gov.MaxExtensions);
    }

    public async Task<RfqDetail> UpdateDraftAsync(Guid id, UpdateRfqDraftRequest req, CancellationToken ct = default)
    {
        var rfq = await Load(id, ct);
        if (rfq.Status != RfqStatus.Draft)
            throw new DomainRuleException("Only a draft RFQ can be edited.");

        ValidateForm(req.FormItems);

        rfq.Title = req.Title;
        rfq.Envelope = ParseEnvelope(req.Envelope);
        rfq.Currency = req.Currency;
        rfq.OpensUtc = req.OpensUtc;
        rfq.ClosesUtc = req.ClosesUtc;
        rfq.Lines = req.Lines.Select(SourcingMapping.ToEntity).ToList();
        rfq.FormItems = req.FormItems.Select((d, i) => SourcingMapping.ToEntity(d, i)).ToList();
        rfq.TechnicalSections = req.TechnicalSections.ToList();
        rfq.CommercialSections = req.CommercialSections.ToList();
        ReconcileDraftInvitations(rfq, req.InvitedVendorIds);
        rfq.TechnicalEvaluatorIds = req.TechnicalEvaluatorIds.Distinct().ToList();
        rfq.CommercialEvaluatorIds = req.CommercialEvaluatorIds.Distinct().ToList();
        rfq.UpdatedUtc = clock.UtcNow;

        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    /// <summary>Draft-stage vendor-picker reconciliation. Adds an invitation for each newly selected
    /// vendor and physically removes de-selected not-yet-acted rows — a Draft invitation is a workspace
    /// selection, not a business fact (approved boundary). No RfqEvent is written for draft churn; the
    /// invited set becomes a fact at release. Once Open, this path is unreachable (edit is Draft-only).</summary>
    private void ReconcileDraftInvitations(Rfq rfq, IReadOnlyList<string> requestedVendorIds)
    {
        var requested = requestedVendorIds
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToHashSet();
        var now = clock.UtcNow;

        foreach (var existing in rfq.Invitations.Select(i => i.VendorId).ToList())
            if (!requested.Contains(existing))
                rfq.RemoveDraftInvitation(existing);

        var current = rfq.Invitations.Select(i => i.VendorId).ToHashSet();
        foreach (var vid in requested)
            if (!current.Contains(vid))
                // Explicit Add: a new invitation reached via the (Modified) Rfq graph would otherwise be
                // detected as Modified because its Guid key is client-set — EF can't tell new from existing.
                db.Add(rfq.InviteVendor(vid, now, _gov.MinRemainingHoursForLateInvite));
    }

    public async Task<RfqDetail> ReleaseAsync(Guid id, CancellationToken ct = default)
    {
        var rfq = await Load(id, ct);
        if (rfq.Status != RfqStatus.Draft)
            throw new DomainRuleException($"RFQ {rfq.Code} is already released.");
        var invitedCount = SourcingMapping.LiveInvitedVendorIds(rfq).Count;
        if (invitedCount == 0)
            throw new DomainRuleException("Invite at least one vendor before releasing.");
        if (rfq.ClosesUtc is null)
            throw new DomainRuleException("Set a bid closing date before releasing.");

        var now = clock.UtcNow;
        // [Slice C] Consolidated lines carry their source PR-line provenance. On release, write one
        // PrLineSourcing link per source and flip each source PR line InDraftRfq → InRfq. Done
        // before the status flip so a stale/changed source line (E1/E2) blocks the whole release.
        await CommitSourcingProvenance(rfq, now, ct);

        // Capture the deadline as a server timestamp and open the RFQ (BUSINESS-RULES [G]).
        rfq.OpensUtc ??= now;
        rfq.OriginalClosesUtc ??= rfq.ClosesUtc;   // immutable baseline for extension analytics (§1.3)
        rfq.MarkReleased(clock.UtcNow);
        rfq.UpdatedUtc = now;
        db.RfqEvents.Add(RfqEvent.Create(rfq.Id, RfqEventType.Released, now, actorUserId: user.UserId));
        await db.SaveChangesAsync(ct);   // status + event in one transaction (G6)

        await audit.WriteAsync("Rfq", rfq.Code, "RFQ released",
            before: "Draft", after: $"Open · closes {rfq.ClosesUtc:u} · {invitedCount} vendor(s)", ct: ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<RfqDetail> CloseAsync(Guid id, CancellationToken ct = default)
    {
        var rfq = await Load(id, ct);
        rfq.CloseEarly(clock.UtcNow);   // guards Open; stamps ClosedUtc (actual). The planned ClosesUtc
                                        // is NOT overwritten (Slice H T5): it stays the planned deadline.
        rfq.UpdatedUtc = clock.UtcNow;
        db.RfqEvents.Add(RfqEvent.Create(rfq.Id, RfqEventType.Closed, clock.UtcNow, actorUserId: user.UserId));
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Rfq", rfq.Code, "Bid window closed early", before: "Open", after: "Closed · ready to open", ct: ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<RfqDetail> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var rfq = await Load(id, ct);
        var before = rfq.Status.ToString();
        rfq.CancelRfq();                // guards not-Awarded/not-Cancelled; same message as before
        rfq.UpdatedUtc = clock.UtcNow;

        // [Slice D] Return this RFQ's sourced PR lines so the demand isn't stranded (A9 / A4).
        await ReturnPrLinesOnCancel(rfq, clock.UtcNow, ct);

        db.RfqEvents.Add(RfqEvent.Create(rfq.Id, RfqEventType.Cancelled, clock.UtcNow, actorUserId: user.UserId));
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Rfq", rfq.Code, "RFQ cancelled", before: before, after: "Cancelled", ct: ct);
        return (await GetAsync(id, ct))!;
    }

    // ===== Buyer governance (RFQ-LIFECYCLE-ADDENDUM §6) =====

    public async Task<RfqDetail> InviteVendorAsync(Guid id, InviteVendorRequest req, CancellationToken ct = default)
    {
        var vendorId = ParseVendorId(req.VendorId);
        var rfq = await Load(id, ct);
        await EnsureVendorExists(vendorId, ct);

        var before = rfq.Invitations.Count;
        var invitation = rfq.InviteVendor(vendorId, clock.UtcNow, _gov.MinRemainingHoursForLateInvite);
        if (rfq.Invitations.Count > before) db.Add(invitation);   // new row (vs T8 re-invite of an existing rescinded row)
        // A live add (Open RFQ) is a governance fact; a Draft add is workspace churn (no event).
        if (rfq.Status == RfqStatus.Open)
            db.RfqEvents.Add(RfqEvent.Create(id, RfqEventType.VendorInvited, clock.UtcNow, vendorId: vendorId, actorUserId: user.UserId));
        rfq.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Rfq", rfq.Code, "Vendor invited", after: invitation.VendorId.ToString(), ct: ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<RfqDetail> RescindInvitationAsync(Guid id, Guid vendorId, RescindInvitationRequest req, CancellationToken ct = default)
    {
        await ValidateReasonAsync("RFQ_RESCIND_REASON", req.ReasonCode, req.Note, ct);
        var rfq = await Load(id, ct);
        var hasSubmittedBid = await db.Bids.AnyAsync(b => b.RfqId == id && b.VendorId == vendorId && b.Submitted, ct);

        rfq.RescindInvitation(vendorId, req.ReasonCode, req.Note, clock.UtcNow, hasSubmittedBid);
        db.RfqEvents.Add(RfqEvent.Create(id, RfqEventType.InvitationRescinded, clock.UtcNow,
            vendorId: vendorId, actorUserId: user.UserId, reasonCode: req.ReasonCode, reasonNote: req.Note));
        rfq.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Rfq", rfq.Code, "Invitation rescinded", after: $"{vendorId} · {req.ReasonCode}", ct: ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<RfqDetail> ExtendAsync(Guid id, ExtendRfqRequest req, CancellationToken ct = default)
    {
        await ValidateReasonAsync("RFQ_EXTENSION_REASON", req.ReasonCode, req.Note, ct);
        var rfq = await Load(id, ct);
        var extensionCount = await db.RfqEvents.CountAsync(e => e.RfqId == id && e.EventType == RfqEventType.Extended, ct);
        var oldCloses = rfq.ClosesUtc;

        rfq.Extend(req.NewClosesUtc, extensionCount, _gov.MaxExtensions, clock.UtcNow);
        db.RfqEvents.Add(RfqEvent.Create(id, RfqEventType.Extended, clock.UtcNow,
            actorUserId: user.UserId, reasonCode: req.ReasonCode, reasonNote: req.Note,
            oldClosesUtc: oldCloses, newClosesUtc: req.NewClosesUtc));
        rfq.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Rfq", rfq.Code, "RFQ extended",
            before: $"{oldCloses:u}", after: $"{req.NewClosesUtc:u} · {req.ReasonCode}", ct: ct);
        return (await GetAsync(id, ct))!;
    }

    private static Guid ParseVendorId(string s) =>
        Guid.TryParse(s, out var g) && g != Guid.Empty ? g : throw new DomainRuleException("A valid vendor id is required.");

    private async Task EnsureVendorExists(Guid vendorId, CancellationToken ct)
    {
        if (!await db.Vendors.AnyAsync(v => v.Id == vendorId, ct))
            throw new NotFoundException($"Vendor {vendorId} not found.");
    }

    /// <summary>Inline reason-code validation against the seeded SYSTEM Custom List (option (b) — matches
    /// the codebase's DomainRuleException→409 convention; no FluentValidation pipeline). Note ≤ 500.</summary>
    private async Task ValidateReasonAsync(string listCode, string reasonCode, string? note, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
            throw new DomainRuleException("A reason code is required.");
        if (note is { Length: > 500 })
            throw new DomainRuleException("The note must be 500 characters or fewer.");
        var list = await customLists.GetAsync(listCode, ct)
            ?? throw new DomainRuleException($"Reason list '{listCode}' is not configured.");
        if (!list.Values.Any(v => v.Code == reasonCode && v.Active))
            throw new DomainRuleException($"'{reasonCode}' is not a valid {list.Name.ToLowerInvariant()}.");
    }

    private async Task<Rfq> Load(Guid id, CancellationToken ct) =>
        await db.Rfqs.Include(x => x.Invitations).FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new NotFoundException($"RFQ {id} not found.");

    /// <summary>
    /// For each consolidated RFQ line, flips its source PR line(s) InDraftRfq → InRfq (the domain
    /// guard rejects any line that isn't claimable — E1/E2) and appends an append-only
    /// <see cref="PrLineSourcing"/> link per source (per-source qty for merges). No-op for RFQs
    /// built via the legacy Confirm-lines path (no provenance), so that flow is unaffected.
    /// </summary>
    private async Task CommitSourcingProvenance(Rfq rfq, DateTime now, CancellationToken ct)
    {
        var sourced = rfq.Lines.Where(l => l.SourcePrLineIds.Count > 0).ToList();
        if (sourced.Count == 0) return;

        var idSet = sourced.SelectMany(l => l.SourcePrLineIds)
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToHashSet();
        var prs = await db.PurchaseRequisitions.Where(p => p.Lines.Any(l => idSet.Contains(l.Id))).ToListAsync(ct);
        var touched = new HashSet<PurchaseRequisition>();

        foreach (var rline in sourced)
        foreach (var sid in rline.SourcePrLineIds)
        {
            if (!Guid.TryParse(sid, out var lineId)) continue;
            var pr = prs.FirstOrDefault(p => p.Lines.Any(l => l.Id == lineId));
            var line = pr?.Lines.FirstOrDefault(l => l.Id == lineId);
            if (pr is null || line is null)
                throw new DomainRuleException($"Source PR line {sid} for {rfq.Code} no longer exists.");

            line.ReleaseToRfq(now);          // InDraftRfq → InRfq, or throws if it was changed meanwhile
            line.Ref = rfq.Code;
            db.PrLineSourcings.Add(new PrLineSourcing(lineId, rfq.Id, rline.LineCode, line.Qty, now));
            touched.Add(pr);
        }

        foreach (var pr in touched) pr.RecomputeHeaderStatus();
    }

    /// <summary>
    /// On RFQ cancel, returns its sourced PR lines to Open. Released-RFQ lines (InRfq, with an
    /// Active link) close the link Cancelled (lineage preserved); a still-draft RFQ's reserved
    /// lines (InDraftRfq, no link yet) are simply released (A9 / A4). No-op for RFQs that sourced
    /// nothing.
    /// </summary>
    private async Task ReturnPrLinesOnCancel(Rfq rfq, DateTime now, CancellationToken ct)
    {
        var links = await db.PrLineSourcings
            .Where(s => s.RfqId == rfq.Id && s.LinkStatus == LinkStatus.Active).ToListAsync(ct);
        var draftSourceIds = rfq.Lines.SelectMany(l => l.SourcePrLineIds)
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToHashSet();
        if (links.Count == 0 && draftSourceIds.Count == 0) return;

        var allIds = links.Select(l => l.PrLineId).Concat(draftSourceIds).ToHashSet();
        var prs = await db.PurchaseRequisitions.Where(p => p.Lines.Any(l => allIds.Contains(l.Id))).ToListAsync(ct);
        var touched = new HashSet<PurchaseRequisition>();
        PrLine? Find(Guid lineId) => prs.SelectMany(p => p.Lines).FirstOrDefault(l => l.Id == lineId);
        PurchaseRequisition? Owner(Guid lineId) => prs.FirstOrDefault(p => p.Lines.Any(l => l.Id == lineId));

        // Released RFQ: InRfq lines → Open, Active link → Cancelled.
        foreach (var link in links)
        {
            var line = Find(link.PrLineId);
            if (line is { LifecycleStatus: PrLineStatus.InRfq })
            {
                line.ReturnFromRfq("RFQ cancelled", now);
                line.Ref = null;
                if (Owner(link.PrLineId) is { } pr) touched.Add(pr);
            }
            link.MarkCancelled("RFQ cancelled", now);
        }

        // Still-draft RFQ: reserved (InDraftRfq) lines → Open, no link was ever written.
        foreach (var sid in draftSourceIds)
        {
            var line = Find(sid);
            if (line is { LifecycleStatus: PrLineStatus.InDraftRfq })
            {
                line.AbandonDraft(now);
                if (Owner(sid) is { } pr) touched.Add(pr);
            }
        }

        foreach (var pr in touched) pr.RecomputeHeaderStatus();
    }

    private static RfqEnvelope ParseEnvelope(string s) =>
        s.Equals("single", StringComparison.OrdinalIgnoreCase) ? RfqEnvelope.Single : RfqEnvelope.Dual;

    internal static void ValidateForm(IEnumerable<FormItemDto> items)
    {
        foreach (var it in items)
        {
            if (!FormItemVocab.IsValidKind(it.Kind))
                throw new DomainRuleException($"Invalid form item kind '{it.Kind}'.");
            if (!FormItemVocab.IsValidGroup(it.Group))
                throw new DomainRuleException($"Invalid form group '{it.Group}'.");
            if (it.Kind == "question" && !FormItemVocab.IsValidType(it.Type))
                throw new DomainRuleException($"Invalid question type '{it.Type}'.");
        }
    }
}
