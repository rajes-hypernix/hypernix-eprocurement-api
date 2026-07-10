using eProcure.Application.Abstractions;
using eProcure.Application.Files;
using eProcure.Domain.Files;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

public sealed class FileStore(AppDbContext db, IClock clock) : IFileStore
{
    public async Task<StoredFileInfo> SaveAsync(string name, string contentType, byte[] content, FileOwnership owner, CancellationToken ct = default)
    {
        var sf = new StoredFile
        {
            Name = string.IsNullOrWhiteSpace(name) ? "upload" : name,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            Content = content,
            Size = content.LongLength,
            CreatedUtc = clock.UtcNow,
            OwnerKind = owner.Kind,
            OwnerVendorId = owner.VendorId,
            OwnerEntityId = owner.EntityId,
        };
        db.StoredFiles.Add(sf);
        await db.SaveChangesAsync(ct);
        return new StoredFileInfo(sf.Id, sf.Name, sf.Size);
    }

    public async Task<StoredFileContent?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var sf = await db.StoredFiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return sf is null ? null : new StoredFileContent(sf.Name, sf.ContentType, sf.Content);
    }
}
