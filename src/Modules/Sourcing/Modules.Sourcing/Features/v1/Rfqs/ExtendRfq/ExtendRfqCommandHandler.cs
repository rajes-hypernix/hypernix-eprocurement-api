using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.ExtendRfq;

public sealed class ExtendRfqCommandHandler(SourcingDbContext dbContext, ICurrentUser currentUser, IOptions<RfqGovernanceOptions> governance)
    : ICommandHandler<ExtendRfqCommand, Guid>
{
    public async ValueTask<Guid> Handle(ExtendRfqCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rfq = await dbContext.Rfqs
            .Include(r => r.Events)
            .FirstOrDefaultAsync(r => r.Id == command.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {command.RfqId} not found.");

        rfq.Extend(
            command.NewClosesUtc,
            governance.Value.MaxExtensions,
            command.ReasonCode,
            command.Note,
            DateTime.UtcNow,
            currentUser.IsAuthenticated() ? currentUser.GetUserId().ToString() : null);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rfq.Id;
    }
}
