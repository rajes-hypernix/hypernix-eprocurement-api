using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using FSH.Modules.Sourcing.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.CloseRfq;

public sealed class CloseRfqCommandHandler(SourcingDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<CloseRfqCommand, Guid>
{
    public async ValueTask<Guid> Handle(CloseRfqCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rfq = await dbContext.Rfqs
            .Include(r => r.Events)
            .FirstOrDefaultAsync(r => r.Id == command.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {command.RfqId} not found.");

        rfq.CloseEarly(DateTime.UtcNow, currentUser.IsAuthenticated() ? currentUser.GetUserId().ToString() : null);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rfq.Id;
    }
}
