using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Evaluation;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Evaluation.SetScore;

public sealed class SetScoreCommandHandler(SourcingDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<SetScoreCommand, TechnicalScoreDto>
{
    public async ValueTask<TechnicalScoreDto> Handle(SetScoreCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rfq = await dbContext.Rfqs
            .FirstOrDefaultAsync(r => r.Id == command.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {command.RfqId} not found.");

        if (!rfq.TechnicalOpened)
        {
            throw new SourcingRuleException("Open the technical envelope before scoring.");
        }

        if (rfq.TechFinalized)
        {
            throw new SourcingRuleException("Scores cannot change after the technical evaluation is finalized.");
        }

        var criterion = Enum.Parse<TechnicalCriterion>(command.Criterion);
        string evaluatorId = currentUser.GetUserId().ToString();

        var score = await dbContext.TechnicalScores
            .FirstOrDefaultAsync(
                s => s.RfqId == command.RfqId && s.VendorId == command.VendorId && s.EvaluatorId == evaluatorId && s.Criterion == criterion,
                cancellationToken)
            .ConfigureAwait(false);

        if (score is null)
        {
            score = TechnicalScore.Create(command.RfqId, command.VendorId, evaluatorId, criterion, command.Score);
            dbContext.TechnicalScores.Add(score);
        }
        else
        {
            score.SetScore(command.Score, rfq.TechFinalized);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new TechnicalScoreDto(score.VendorId, score.Criterion.ToString(), score.Score);
    }
}
