namespace eProcure.Application.Files;

public sealed record StoredFileInfo(Guid Id, string Name, long Size);
public sealed record StoredFileContent(string Name, string ContentType, byte[] Content);

public interface IFileStore
{
    Task<StoredFileInfo> SaveAsync(string name, string contentType, byte[] content, CancellationToken ct = default);
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
