namespace FSH.Modules.Suppliers.Data;

public interface ISuppliersCodeGenerator
{
    Task<string> NextVendorCodeAsync(CancellationToken cancellationToken);

    Task<string> NextVendorUserCodeAsync(CancellationToken cancellationToken);

    Task<string> NextOnboardingApplicationCodeAsync(CancellationToken cancellationToken);
}
