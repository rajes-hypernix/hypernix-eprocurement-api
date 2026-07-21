namespace FSH.Modules.Sourcing.Domain;

/// <summary>Filename only — matches the old system's own minimalism; real attachment storage was never finished there either.</summary>
public sealed class BidAttachment
{
    public string FileName { get; private set; }

    public BidAttachment(string fileName)
    {
        FileName = fileName;
    }
}
