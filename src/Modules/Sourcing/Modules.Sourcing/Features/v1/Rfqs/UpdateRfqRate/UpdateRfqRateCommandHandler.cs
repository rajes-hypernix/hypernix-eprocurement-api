using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using FSH.Modules.Sourcing.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.UpdateRfqRate;

public sealed class UpdateRfqRateCommandHandler(SourcingDbContext dbContext, IMediator mediator)
    : ICommandHandler<UpdateRfqRateCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateRfqRateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rfq = await dbContext.Rfqs
            .FirstOrDefaultAsync(r => r.Id == command.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {command.RfqId} not found.");

        var rates = await mediator.Send(new ListCurrentExchangeRatesQuery(), cancellationToken).ConfigureAwait(false);
        rfq.SetExchangeRateToBase(RfqExchangeRateSupport.ResolveRateToBase(rfq.Currency, rates));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rfq.Id;
    }
}
