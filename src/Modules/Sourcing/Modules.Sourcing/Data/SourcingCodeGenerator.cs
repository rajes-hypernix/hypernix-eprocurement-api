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

    public async Task<string> NextBidCodeAsync(CancellationToken cancellationToken)
    {
        long next = await NextValueAsync("BidCodeSeq", cancellationToken).ConfigureAwait(false);
        return $"BID-{DateTime.UtcNow.Year}-{next:D4}";
    }

    public async Task<string> NextAwardCodeAsync(CancellationToken cancellationToken)
    {
        long next = await NextValueAsync("AwardCodeSeq", cancellationToken).ConfigureAwait(false);
        return $"AWD-{DateTime.UtcNow.Year}-{next:D4}";
    }

    private async Task<long> NextValueAsync(string sequenceName, CancellationToken cancellationToken)
    {
        string qualifiedName = $"{SourcingDbContext.Schema}.\"{sequenceName}\"";
        // EF Core SqlQuery<T> projects primitives as a column named "Value".
        return await dbContext.Database
            .SqlQuery<long>($"SELECT nextval({qualifiedName}) AS \"Value\"")
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
