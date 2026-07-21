using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Data;

public sealed class ProcurementCodeGenerator(ProcurementDbContext dbContext) : IProcurementCodeGenerator
{
    public async Task<string> NextPoCodeAsync(CancellationToken cancellationToken)
    {
        long next = await NextValueAsync("PoCodeSeq", cancellationToken).ConfigureAwait(false);
        return $"PO-{DateTime.UtcNow.Year}-{next:D4}";
    }

    public async Task<string> NextAsnCodeAsync(CancellationToken cancellationToken)
    {
        long next = await NextValueAsync("AsnCodeSeq", cancellationToken).ConfigureAwait(false);
        return $"ASN-{DateTime.UtcNow.Year}-{next:D4}";
    }

    public async Task<string> NextGrnCodeAsync(CancellationToken cancellationToken)
    {
        long next = await NextValueAsync("GrnCodeSeq", cancellationToken).ConfigureAwait(false);
        return $"GRN-{DateTime.UtcNow.Year}-{next:D4}";
    }

    public async Task<string> NextInvoiceCodeAsync(CancellationToken cancellationToken)
    {
        long next = await NextValueAsync("InvCodeSeq", cancellationToken).ConfigureAwait(false);
        return $"INV-{DateTime.UtcNow.Year}-{next:D4}";
    }

    private async Task<long> NextValueAsync(string sequenceName, CancellationToken cancellationToken)
    {
        string qualifiedName = $"{ProcurementDbContext.Schema}.\"{sequenceName}\"";
        return await dbContext.Database
            .SqlQuery<long>($"SELECT nextval({qualifiedName}) AS \"Value\"")
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
