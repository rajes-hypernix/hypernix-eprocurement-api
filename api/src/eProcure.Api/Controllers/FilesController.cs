using eProcure.Api.Auth;
using eProcure.Application.Abstractions;
using eProcure.Application.Authorization;
using eProcure.Application.Files;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace eProcure.Api.Controllers;

// All endpoints require an authenticated principal via the fallback policy (SEC-1). Download adds
// resource scoping + a probity audit trail (SEC-3).
[ApiController]
[Route("api/files")]
public sealed class FilesController(IFileStore files, IFileAccessPolicy access, IAuditLog audit, ICurrentUser user) : ControllerBase
{
    [HttpPost]
    [Action(ApiActions.UploadFile)]
    [RequestSizeLimit(20_000_000)]   // 20 MB cap for demo uploads
    public async Task<ActionResult<StoredFileInfo>> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("No file provided.");
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        // Stamp ownership at upload (T5): a vendor's generic upload is a bid attachment owned by them;
        // an internal upload is Internal (buyer/admin only).
        var owner = user.VendorId is { } vid ? FileOwnership.Bid(vid) : FileOwnership.Internal;
        return Ok(await files.SaveAsync(file.FileName, file.ContentType, ms.ToArray(), owner, ct));
    }

    [HttpGet("{id:guid}")]
    [Action(ApiActions.DownloadFile)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        // Resource scoping: a vendor may download only their own files; internal reviewers, any
        // (SEC-3). Deny-on-uncertainty — an unprovable owner is a 403, not an allow.
        if (!await access.CanDownloadAsync(id, ct))
            return Forbid();

        var f = await files.GetAsync(id, ct);
        if (f is null) return NotFound();

        // Probity requirement (O&G): record who accessed a stored document — sealed-bid attachments
        // in particular must carry an access trail.
        await audit.WriteAsync("File", id.ToString(), "Downloaded", after: f.Name, ct: ct);

        // inline so the browser previews PDFs/images. SetHttpFileName RFC-6266-encodes the
        // name (filename + filename*) so non-ASCII chars — e.g. the U+202F in macOS
        // screenshot names — don't blow up Kestrel's header validation.
        var cd = new ContentDispositionHeaderValue("inline");
        cd.SetHttpFileName(f.Name);
        Response.Headers[HeaderNames.ContentDisposition] = cd.ToString();
        var contentType = string.IsNullOrWhiteSpace(f.ContentType) ? "application/octet-stream" : f.ContentType;
        return File(f.Content, contentType);
    }
}
