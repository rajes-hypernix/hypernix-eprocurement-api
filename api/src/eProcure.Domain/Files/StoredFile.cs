namespace eProcure.Domain.Files;

/// <summary>
/// A user-uploaded file (bid attachments, etc.) stored as bytes so it can be served
/// back for download. Referenced from answer values by its <see cref="Id"/>.
/// </summary>
public class StoredFile
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = [];
    public long Size { get; set; }
    public DateTime CreatedUtc { get; set; }
}
