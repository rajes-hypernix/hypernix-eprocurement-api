using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Data;

public sealed class SourcingCodeGenerator(SourcingDbContext dbContext) : ISourcingCodeGenerator
{
    public async Task<string> NextPurchaseRequisitionCodeAsync(CancellationToken cancellationToken)
    {
        long next = await NextValueAsync("PrCodeSeq", cancellationToken).ConfigureAwait(false);
        return $"PR-{DateTime.UtcNow.Year}-{next:D4}";
    }

    public async Task<string> NextRfqCodeAsync(CancellationToken cancellationToken)
    {
        long next = await NextValueAsync("RfqCodeSeq", cancellationToken).ConfigureAwait(false);
        return $"RFQ-{DateTime.UtcNow.Year}-{next:D4}";
    }

    private async Task<long> NextValueAsync(string sequenceName, CancellationToken cancellationToken)
    {
        string qualifiedName = $"{SourcingDbContext.Schema}.\"{sequenceName}\"";
        return await dbContext.Database
            .SqlQuery<long>($"SELECT nextval({qualifiedName})")
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
