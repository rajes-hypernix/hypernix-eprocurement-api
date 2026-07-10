using eProcure.Application.Abstractions;
using eProcure.Application.Communication;
using eProcure.Domain.Communication;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

public sealed class ClarificationService(AppDbContext db, ICurrentUser user, IClock clock) : IClarificationService
{
    private bool IsVendor => user.VendorId is not null;
    private string MyKind => IsVendor ? "vendor" : "buyer";

    public async Task<IReadOnlyList<ClarificationThreadDto>> ListThreadsAsync(CancellationToken ct = default)
    {
        var q = db.Clarifications.AsNoTracking().AsQueryable();
        if (user.VendorId is { } vid) q = q.Where(c => c.VendorId == vid);   // [G] vendor scoping
        var msgs = await q.ToListAsync(ct);

        var vendorNames = await VendorNames(msgs.Select(m => m.VendorId).Distinct(), ct);
        var scopeLabels = await ScopeLabels(msgs.Select(m => m.Scope).Distinct(), ct);

        return msgs
            .GroupBy(m => (m.Scope, m.VendorId))
            .Select(g =>
            {
                var ordered = g.OrderBy(m => m.CreatedUtc).ToList();
                var last = ordered[^1];
                var general = g.Key.Scope == "general";
                return new ClarificationThreadDto(
                    g.Key.Scope, g.Key.VendorId,
                    vendorNames.GetValueOrDefault(g.Key.VendorId, "Vendor"),
                    general ? "General inquiry" : scopeLabels.GetValueOrDefault(g.Key.Scope, g.Key.Scope),
                    general,
                    string.IsNullOrWhiteSpace(last.Body) ? "📎 attachment" : last.Body,
                    last.CreatedUtc,
                    Unread(ordered));
            })
            .OrderByDescending(t => t.LastUtc)
            .ToList();
    }

    public async Task<ClarificationThreadDetail?> GetThreadAsync(string scope, Guid vendorId, CancellationToken ct = default)
    {
        if (user.VendorId is { } own && own != vendorId)
            throw new UnauthorizedAccessException("Vendors can only view their own clarification threads.");

        var msgs = await db.Clarifications
            .Where(c => c.Scope == scope && c.VendorId == vendorId)
            .OrderBy(c => c.CreatedUtc).ToListAsync(ct);
        if (msgs.Count == 0) return null;

        // Opening the thread marks the counterparty's messages read for my side.
        foreach (var m in msgs)
        {
            if (IsVendor) m.ReadByVendor = true;
            else m.ReadByBuyer = true;
        }
        await db.SaveChangesAsync(ct);

        return await BuildDetail(scope, vendorId, msgs, ct);
    }

    public async Task<ClarificationThreadDetail> SendAsync(SendClarificationRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Body))
            throw new InvalidOperationException("Message body is required.");
        if (user.VendorId is { } own && own != req.VendorId)
            throw new UnauthorizedAccessException("Vendors can only message procurement on their own threads.");

        var now = clock.UtcNow;
        var senderName = IsVendor
            ? (await VendorNames(new[] { req.VendorId }, ct)).GetValueOrDefault(req.VendorId, user.UserName ?? "Vendor")
            : (user.UserName ?? "Procurement");

        // Buyer "publish to all bidders" on an RFQ scope fans the answer out (anonymised)
        // to every invited vendor's thread; otherwise it's a single targeted message.
        var targets = new List<Guid> { req.VendorId };
        var published = req.Published && !IsVendor && req.Scope != "general";
        if (published)
        {
            var rfq = await db.Rfqs.AsNoTracking().Include(r => r.Invitations).FirstOrDefaultAsync(r => r.Code == req.Scope, ct);
            if (rfq is not null)
            {
                var invited = SourcingMapping.LiveInvitedVendorIds(rfq)
                    .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                    .Where(g => g != Guid.Empty);
                targets = invited.Append(req.VendorId).Distinct().ToList();
            }
        }

        foreach (var t in targets)
        {
            db.Clarifications.Add(new Clarification
            {
                Scope = req.Scope,
                VendorId = t,
                SenderKind = MyKind,
                SenderName = senderName,
                // Only the vendor's own targeted message records an addressee; broadcast fan-out has none.
                RecipientUserId = IsVendor && t == req.VendorId ? req.RecipientUserId : null,
                Body = req.Body,
                Published = published,
                CreatedUtc = now,
                ReadByBuyer = !IsVendor,
                ReadByVendor = IsVendor && t == req.VendorId,
            });
        }
        await db.SaveChangesAsync(ct);

        var msgs = await db.Clarifications
            .Where(c => c.Scope == req.Scope && c.VendorId == req.VendorId)
            .OrderBy(c => c.CreatedUtc).ToListAsync(ct);
        return await BuildDetail(req.Scope, req.VendorId, msgs, ct);
    }

    // ---- helpers ----
    private int Unread(IEnumerable<Clarification> msgs) => msgs.Count(m =>
        m.SenderKind != MyKind && (IsVendor ? !m.ReadByVendor : !m.ReadByBuyer));

    private async Task<ClarificationThreadDetail> BuildDetail(string scope, Guid vendorId, List<Clarification> msgs, CancellationToken ct)
    {
        var vendorName = (await VendorNames(new[] { vendorId }, ct)).GetValueOrDefault(vendorId, "Vendor");
        var general = scope == "general";
        var label = general ? "General inquiry" : (await ScopeLabels(new[] { scope }, ct)).GetValueOrDefault(scope, scope);

        var recipientIds = msgs.Select(m => m.RecipientUserId).Where(r => !string.IsNullOrEmpty(r)).Distinct().ToList();
        var recipientNames = recipientIds.Count == 0 ? [] : await db.Users.AsNoTracking()
            .Where(u => recipientIds.Contains(u.Code)).ToDictionaryAsync(u => u.Code, u => u.Name, ct);

        return new ClarificationThreadDetail(scope, vendorId, vendorName, label, general,
            msgs.Select(m => new ClarificationMessageDto(
                m.Id, m.SenderKind, m.SenderName, m.Body, m.Published, m.CreatedUtc, m.SenderKind == MyKind,
                m.RecipientUserId is null ? null : recipientNames.GetValueOrDefault(m.RecipientUserId))).ToList());
    }

    private async Task<Dictionary<Guid, string>> VendorNames(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var set = ids.ToHashSet();
        return await db.Vendors.AsNoTracking().Where(v => set.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.Name, ct);
    }

    private async Task<Dictionary<string, string>> ScopeLabels(IEnumerable<string> scopes, CancellationToken ct)
    {
        var codes = scopes.Where(s => s != "general").ToHashSet();
        return await db.Rfqs.AsNoTracking().Where(r => codes.Contains(r.Code))
            .ToDictionaryAsync(r => r.Code, r => r.Title, ct);
    }
}
