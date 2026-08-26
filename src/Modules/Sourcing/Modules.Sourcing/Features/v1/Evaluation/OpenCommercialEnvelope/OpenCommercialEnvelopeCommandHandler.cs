using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.v1.Evaluation;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Evaluation.OpenCommercialEnvelope;

public sealed class OpenCommercialEnvelopeCommandHandler(SourcingDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<OpenCommercialEnvelopeCommand, Guid>
{
    public async ValueTask<Guid> Handle(OpenCommercialEnvelopeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rfq = await dbContext.Rfqs
            .FirstOrDefaultAsync(r => r.Id == command.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {command.RfqId} not found.");

        if (rfq.Envelope == RfqEnvelope.Dual)
        {
            string me = currentUser.GetUserId().ToString();
            if (!rfq.CommercialEvaluatorIds.Any(id => string.Equals(id, me, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ForbiddenException("Only an assigned commercial evaluator can open the commercial envelope.");
            }
        }

        rfq.OpenCommercialEnvelope();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rfq.Id;
    }
}
