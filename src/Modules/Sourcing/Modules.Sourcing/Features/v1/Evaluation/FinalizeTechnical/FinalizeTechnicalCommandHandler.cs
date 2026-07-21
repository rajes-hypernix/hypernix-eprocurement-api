using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.v1.Evaluation;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Evaluation.FinalizeTechnical;

public sealed class FinalizeTechnicalCommandHandler(SourcingDbContext dbContext)
    : ICommandHandler<FinalizeTechnicalCommand, Guid>
{
    public async ValueTask<Guid> Handle(FinalizeTechnicalCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rfq = await dbContext.Rfqs
            .Include(r => r.Invitations)
            .FirstOrDefaultAsync(r => r.Id == command.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {command.RfqId} not found.");

        var submittedVendorIds = rfq.Invitations
            .Where(i => i.Status == RfqInvitationStatus.BidSubmitted)
            .Select(i => i.VendorId)
            .ToList();
        bool hasSubmittedBids = submittedVendorIds.Count > 0;

        var scores = await dbContext.TechnicalScores
            .AsNoTracking()
            .Where(s => s.RfqId == command.RfqId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool allScored = hasSubmittedBids && submittedVendorIds.TrueForAll(vendorId =>
            TechnicalEvaluation.Committee([.. scores.Where(s => s.VendorId == vendorId)]) is not null);

        rfq.FinalizeTechnical(hasSubmittedBids, allScored);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rfq.Id;
    }
}
