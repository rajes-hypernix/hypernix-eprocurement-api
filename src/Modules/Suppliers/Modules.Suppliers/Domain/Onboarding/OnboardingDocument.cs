namespace FSH.Modules.Suppliers.Domain.Onboarding;

/// <summary>
/// An uploaded onboarding document (SSM, ISO, bank letter, ...). The bytes live in blob storage
/// (via the shared Storage building block — the vendor-facing upload is anonymous/token-scoped,
/// so it bypasses the Files module's authenticated presigned-upload flow); this holds the
/// checklist key, the file name, and the storage path.
/// </summary>
public sealed class OnboardingDocument
{
    public string Key { get; private set; }
    public string FileName { get; private set; }
    public string StorageKey { get; private set; }
    public DateTime UploadedUtc { get; private set; }

    public OnboardingDocument(string key, string fileName, string storageKey, DateTime uploadedUtc)
    {
        Key = key;
        FileName = fileName;
        StorageKey = storageKey;
        UploadedUtc = uploadedUtc;
    }
}
