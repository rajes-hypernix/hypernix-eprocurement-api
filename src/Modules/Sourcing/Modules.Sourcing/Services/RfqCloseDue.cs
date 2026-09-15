using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;

namespace FSH.Modules.Sourcing.Services;

internal static class RfqCloseDue
{
    internal static bool Apply(Rfq rfq, DateTime nowUtc) => rfq.CloseIfDue(nowUtc) is not null;

    internal static async Task PersistAsync(SourcingDbContext dbContext, Rfq rfq, CancellationToken cancellationToken)
    {
        if (!Apply(rfq, DateTime.UtcNow))
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
