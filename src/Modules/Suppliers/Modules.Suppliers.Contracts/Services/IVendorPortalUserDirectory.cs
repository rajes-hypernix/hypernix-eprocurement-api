namespace FSH.Modules.Suppliers.Contracts.Services;

/// <summary>Resolves portal Identity user ids for vendor notification fan-out.</summary>
public interface IVendorPortalUserDirectory
{
    Task<IReadOnlyList<string>> GetIdentityUserIdsForVendorsAsync(
        IReadOnlyList<Guid> vendorIds,
        CancellationToken cancellationToken = default);
}
