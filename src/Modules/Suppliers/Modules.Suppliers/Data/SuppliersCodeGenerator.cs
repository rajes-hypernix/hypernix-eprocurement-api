using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Data;

public sealed class SuppliersCodeGenerator(SuppliersDbContext dbContext) : ISuppliersCodeGenerator
{
    public async Task<string> NextVendorCodeAsync(CancellationToken cancellationToken)
    {
        long next = await NextValueAsync("VendorCodeSeq", cancellationToken).ConfigureAwait(false);
        return $"V-{next:D5}";
    }

    public async Task<string> NextVendorUserCodeAsync(CancellationToken cancellationToken)
    {
        long next = await NextValueAsync("VendorUserCodeSeq", cancellationToken).ConfigureAwait(false);
        return $"VU-{next:D5}";
    }

    public async Task<string> NextOnboardingApplicationCodeAsync(CancellationToken cancellationToken)
    {
        long next = await NextValueAsync("OnboardingApplicationCodeSeq", cancellationToken).ConfigureAwait(false);
        return $"VOB-{DateTime.UtcNow.Year}-{next:D4}";
    }

    private async Task<long> NextValueAsync(string sequenceName, CancellationToken cancellationToken)
    {
        string qualifiedName = $"{SuppliersDbContext.Schema}.\"{sequenceName}\"";
        // EF Core SqlQuery<T> projects primitives as a column named "Value".
        return await dbContext.Database
            .SqlQuery<long>($"SELECT nextval({qualifiedName}) AS \"Value\"")
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
