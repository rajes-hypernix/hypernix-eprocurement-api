using eProcure.Application.Abstractions;
using eProcure.Application.Files;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// Resource-scopes file downloads (SEC-3). Internal principals may read any file; a vendor principal
/// may read only files whose typed <c>OwnerVendorId</c> is their own (T5 retired the answer-value
/// inference). Fail-closed: an unknown file, or one with no matching owner, is refused for vendors.
/// </summary>
public sealed class FileAccessPolicy(AppDbContext db, ICurrentUser user) : IFileAccessPolicy
{
    public async Task<bool> CanDownloadAsync(Guid fileId, CancellationToken ct = default)
    {
        // Not a vendor principal ⇒ internal (buyer/admin/evaluator); the fallback policy already
        // guaranteed authentication. Internal reviewers legitimately read any bid/onboarding file.
        if (user.VendorId is not { } vendorId)
            return true;

        // Vendor: allowed only when the file's typed owner is this vendor. A missing file or a null /
        // different OwnerVendorId (Internal, or an unpromoted onboarding upload) is a fail-closed 403.
        var ownerVendorId = await db.StoredFiles.AsNoTracking()
            .Where(f => f.Id == fileId)
            .Select(f => f.OwnerVendorId)
            .FirstOrDefaultAsync(ct);

        return ownerVendorId == vendorId;
    }
}
