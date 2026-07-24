namespace FSH.Modules.Suppliers.Contracts.Services;

/// <summary>Deliberately whitelist-only: <see cref="Code"/>/<see cref="Name"/>. Bank accounts, tax
/// id, contacts and every other Vendor field are not exposed through this seam — a security
/// boundary, not a convenience, matching the old source's rule that vendor banking detail can never
/// be reached through a one-hop related-field join. Extend this record only by adding another
/// explicitly-approved public field, never by widening it into a general Vendor projection.</summary>
public sealed record VendorLookupDto(Guid Id, string Code, string Name);

/// <summary>Cross-module read seam other modules use for one-hop related fields (e.g. a Purchase
/// Order's Vendor name in a saved view) without depending on Suppliers' own schema.</summary>
public interface IVendorLookupService
{
    Task<IReadOnlyDictionary<Guid, VendorLookupDto>> GetManyAsync(IReadOnlyList<Guid> vendorIds, CancellationToken ct);
}
