using eProcure.Application.Abstractions;
using eProcure.Application.Files;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// Resource-scopes file downloads (SEC-3). Internal principals may read any file; a vendor principal
/// may read only files reachable from their own bids or their promoted onboarding application.
/// StoredFile has no owner column, so ownership is inferred by scanning the "<fileId>::<name>|…"
/// answer-value convention plus the typed OnboardingDocument.StoredFileId — and the policy DENIES on
/// uncertainty (a GUID not provably owned by the vendor is refused, never allowed).
/// </summary>
public sealed class FileAccessPolicy(AppDbContext db, ICurrentUser user) : IFileAccessPolicy
{
    public async Task<bool> CanDownloadAsync(Guid fileId, CancellationToken ct = default)
    {
        // Not a vendor principal ⇒ internal (buyer/admin/evaluator); the fallback policy already
        // guaranteed authentication. Internal reviewers legitimately read any bid/onboarding file.
        if (user.VendorId is not { } vendorId)
            return true;

        var owned = await OwnedFileIdsAsync(vendorId, ct);
        return owned.Contains(fileId);
    }

    /// <summary>Every file GUID this vendor can prove ownership of.</summary>
    private async Task<HashSet<Guid>> OwnedFileIdsAsync(Guid vendorId, CancellationToken ct)
    {
        var ids = new HashSet<Guid>();

        // 1. Bid answer attachments ("<fileId>::<name>" entries, pipe-separated) on the vendor's bids.
        var bids = await db.Bids.AsNoTracking().Where(b => b.VendorId == vendorId).ToListAsync(ct);
        foreach (var bid in bids)
            foreach (var answer in bid.Answers)
                AddFileRefs(ids, answer.Value);

        // 2. The vendor's promoted onboarding application: typed documents + any answer attachments.
        var apps = await db.VendorOnboardingApplications.AsNoTracking()
            .Where(a => a.PromotedVendorId == vendorId).ToListAsync(ct);
        foreach (var app in apps)
        {
            foreach (var doc in app.Documents)
                ids.Add(doc.StoredFileId);
            foreach (var answer in app.Answers)
                AddFileRefs(ids, answer.Value);
        }

        return ids;
    }

    /// <summary>Parses file GUIDs out of an answer value using the "<fileId>::<name>|…" convention.</summary>
    private static void AddFileRefs(HashSet<Guid> ids, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        foreach (var entry in value.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var idPart = entry.Split("::", 2)[0];
            if (Guid.TryParse(idPart, out var g)) ids.Add(g);
        }
    }
}
