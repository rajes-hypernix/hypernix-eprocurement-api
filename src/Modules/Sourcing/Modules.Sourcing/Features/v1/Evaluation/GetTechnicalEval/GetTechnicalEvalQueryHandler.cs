using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Evaluation;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Evaluation.GetTechnicalEval;

/// <summary>
/// Masking rule: a caller who lacks <see cref="SourcingPermissions.Award.View"/> (a pure
/// evaluator, not a Buyer/Approver/Admin) sees only the "Bidder A/B/C" alias — real vendor
/// identity stays sealed until commercial reveal.
/// </summary>
public sealed class GetTechnicalEvalQueryHandler(
    SourcingDbContext dbContext,
    IMediator mediator,
    ICurrentUser currentUser,
    IUserService userService)
    : IQueryHandler<GetTechnicalEvalQuery, IReadOnlyList<TechnicalEvalRowDto>>
{
    public async ValueTask<IReadOnlyList<TechnicalEvalRowDto>> Handle(GetTechnicalEvalQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rfq = await dbContext.Rfqs
            .AsNoTracking()
            .Include(r => r.Invitations)
            .FirstOrDefaultAsync(r => r.Id == query.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {query.RfqId} not found.");

        var scores = await dbContext.TechnicalScores
            .AsNoTracking()
            .Where(s => s.RfqId == query.RfqId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool masked = !await userService
            .HasPermissionAsync(currentUser.GetUserId().ToString(), SourcingPermissions.Award.View, cancellationToken)
            .ConfigureAwait(false);

        var invitedOrder = rfq.Invitations.OrderBy(i => i.InvitedUtc).Select(i => i.VendorId).ToList();
        var submittedVendorIds = rfq.Invitations
            .Where(i => i.Status == RfqInvitationStatus.BidSubmitted)
            .Select(i => i.VendorId)
            .ToList();

        var rows = new List<TechnicalEvalRowDto>();
        foreach (var vendorId in submittedVendorIds)
        {
            var vendorScores = scores.Where(s => s.VendorId == vendorId).ToList();
            var committee = TechnicalEvaluation.Committee(vendorScores);
            string alias = TechnicalEvaluation.Alias(invitedOrder, vendorId);

            string? vendorName = null;
            string? vendorCode = null;
            if (!masked)
            {
                try
                {
                    var vendor = await mediator.Send(new GetVendorByIdQuery(vendorId), cancellationToken).ConfigureAwait(false);
                    vendorName = vendor.Name;
                    vendorCode = vendor.Code;
                }
                catch (NotFoundException)
                {
                    vendorName = "(unknown vendor)";
                }
            }

            rows.Add(new TechnicalEvalRowDto(
                vendorId,
                vendorName,
                vendorCode,
                alias,
                masked,
                committee,
                TechnicalEvaluation.Pass(committee),
                [.. vendorScores.Select(s => new TechnicalScoreDetailDto(s.EvaluatorId, s.Criterion.ToString(), s.Score))]));
        }

        return rows;
    }
}
