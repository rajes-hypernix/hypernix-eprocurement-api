using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.v1.Evaluation;
using FSH.Modules.Sourcing.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Evaluation.OpenCommercialEnvelope;

public sealed class OpenCommercialEnvelopeCommandHandler(SourcingDbContext dbContext)
    : ICommandHandler<OpenCommercialEnvelopeCommand, Guid>
{
    public async ValueTask<Guid> Handle(OpenCommercialEnvelopeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rfq = await dbContext.Rfqs
            .FirstOrDefaultAsync(r => r.Id == command.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {command.RfqId} not found.");

        rfq.OpenCommercialEnvelope();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rfq.Id;
    }
}
