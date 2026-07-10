using eProcure.Domain.Files;

namespace eProcure.Application.Files;

public sealed record StoredFileInfo(Guid Id, string Name, long Size);
public sealed record StoredFileContent(string Name, string ContentType, byte[] Content);

/// <summary>Typed ownership stamped on a file at upload time (T5), so downloads scope by column, not
/// by scanning answer values.</summary>
public sealed record FileOwnership(FileOwnerKind Kind, Guid? VendorId, Guid? EntityId)
{
    /// <summary>Buyer/admin upload — readable only by internal principals.</summary>
    public static readonly FileOwnership Internal = new(FileOwnerKind.Internal, null, null);

    /// <summary>A vendor's bid attachment.</summary>
    public static FileOwnership Bid(Guid vendorId) => new(FileOwnerKind.Bid, vendorId, null);

    /// <summary>An onboarding document (anonymous, token-scoped — no vendor principal yet).</summary>
    public static FileOwnership OnboardingDocument(Guid applicationId) =>
        new(FileOwnerKind.OnboardingDocument, null, applicationId);
}

public interface IFileStore
{
    Task<StoredFileInfo> SaveAsync(string name, string contentType, byte[] content, FileOwnership owner, CancellationToken ct = default);
    Task<StoredFileContent?> GetAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Decides whether the current principal may download a given stored file (SEC-3). Internal
/// principals (buyer/admin/evaluators) may read any file; a vendor principal may read only files
/// reachable from their own bids or their promoted onboarding application. Ownership is inferred by
/// reverse-lookup because StoredFile carries no owner column — so the policy DENIES on uncertainty.
/// </summary>
public interface IFileAccessPolicy
{
    Task<bool> CanDownloadAsync(Guid fileId, CancellationToken ct = default);
}
