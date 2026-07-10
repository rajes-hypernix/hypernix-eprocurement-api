namespace eProcure.Domain.Files;

/// <summary>What a stored file belongs to (SEC-3 / Slice G T5). Drives download scoping without the
/// fragile answer-value inference the policy used before.</summary>
public enum FileOwnerKind { Bid, OnboardingDocument, OnboardingAnswer, Internal }

/// <summary>
/// A user-uploaded file (bid attachments, etc.) stored as bytes so it can be served back for
/// download. Ownership is now a typed fact (<see cref="OwnerKind"/> / <see cref="OwnerVendorId"/> /
/// <see cref="OwnerEntityId"/>) rather than inferred from answer values.
/// </summary>
public class StoredFile
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = [];
    public long Size { get; set; }
    public DateTime CreatedUtc { get; set; }

    /// <summary>What the file belongs to. Defaults to <see cref="FileOwnerKind.Internal"/> (buyer/admin
    /// only) so an unattributed file is fail-closed for vendors.</summary>
    public FileOwnerKind OwnerKind { get; set; } = FileOwnerKind.Internal;

    /// <summary>The vendor that owns the file, if any (null for Internal or an unpromoted onboarding
    /// upload). A vendor principal may download only files whose OwnerVendorId is their own.</summary>
    public Guid? OwnerVendorId { get; set; }

    /// <summary>The owning record (bid id / onboarding application id), for lineage. Nullable.</summary>
    public Guid? OwnerEntityId { get; set; }
}
