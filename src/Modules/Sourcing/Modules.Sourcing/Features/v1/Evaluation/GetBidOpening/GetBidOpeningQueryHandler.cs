using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Identity.Contracts.DTOs;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Evaluation;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Evaluation.GetBidOpening;

public sealed class GetBidOpeningQueryHandler(
    SourcingDbContext dbContext,
    ICurrentUser currentUser,
    IUserService userService)
    : IQueryHandler<GetBidOpeningQuery, BidOpeningStatusDto>
{
    public async ValueTask<BidOpeningStatusDto> Handle(GetBidOpeningQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rfq = await dbContext.Rfqs
            .AsNoTracking()
            .Include(r => r.Invitations)
            .FirstOrDefaultAsync(r => r.Id == query.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {query.RfqId} not found.");

        int submittedCount = await dbContext.Bids
            .AsNoTracking()
            .CountAsync(b => b.RfqId == query.RfqId && b.Submitted, cancellationToken)
            .ConfigureAwait(false);

        string me = currentUser.IsAuthenticated() ? currentUser.GetUserId().ToString() : string.Empty;
        bool windowClosed = rfq.Status is not (RfqStatus.Draft or RfqStatus.Open);
        bool canTech = rfq.Envelope == RfqEnvelope.Dual
            && !rfq.TechnicalOpened
            && windowClosed
            && IsAssigned(rfq.TechnicalEvaluatorIds, me);
        bool canComm = !rfq.CommercialOpened
            && (rfq.Envelope == RfqEnvelope.Single
                ? windowClosed
                : rfq.TechFinalized && IsAssigned(rfq.CommercialEvaluatorIds, me));

        var evaluators = new List<EvaluatorDto>(rfq.TechnicalEvaluatorIds.Count);
        foreach (var id in rfq.TechnicalEvaluatorIds)
        {
            evaluators.Add(new EvaluatorDto(id, await ResolveNameAsync(id, cancellationToken).ConfigureAwait(false)));
        }

        return new BidOpeningStatusDto(
            rfq.Id,
            rfq.Code,
            rfq.Title,
            rfq.Envelope.ToString(),
            rfq.Status.ToString(),
            rfq.TechnicalOpened,
            rfq.TechFinalized,
            rfq.CommercialOpened,
            rfq.Invitations.Count,
            submittedCount,
            evaluators,
            canTech,
            canComm);
    }

    private static bool IsAssigned(IReadOnlyList<string> ids, string userId) =>
        !string.IsNullOrEmpty(userId)
        && ids.Any(id => string.Equals(id, userId, StringComparison.OrdinalIgnoreCase));

    private async Task<string> ResolveNameAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            UserDto user = await userService.GetAsync(userId, cancellationToken).ConfigureAwait(false);
            string name = $"{user.FirstName} {user.LastName}".Trim();
            return string.IsNullOrEmpty(name) ? (user.UserName ?? userId) : name;
        }
        catch (Exception)
        {
            return userId;
        }
    }
}
