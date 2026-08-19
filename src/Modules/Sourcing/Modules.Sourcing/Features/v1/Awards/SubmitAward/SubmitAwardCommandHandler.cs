using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Awards;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Awards.SubmitAward;

public sealed class SubmitAwardCommandHandler(
    SourcingDbContext dbContext,
    ISourcingCodeGenerator codeGenerator,
    ICurrentUser currentUser)
    : ICommandHandler<SubmitAwardCommand, AwardDto>
{
    public async ValueTask<AwardDto> Handle(SubmitAwardCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rfq = await dbContext.Rfqs
            .Include(r => r.Invitations)
            .FirstOrDefaultAsync(r => r.Id == command.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {command.RfqId} not found.");

        var vendorIds = command.Allocations.Select(a => a.VendorId).Distinct().ToList();

        var bids = await dbContext.Bids
            .Where(b => b.RfqId == command.RfqId && vendorIds.Contains(b.VendorId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var bidsByVendor = bids.ToDictionary(b => b.VendorId);

        var scores = await dbContext.TechnicalScores
            .AsNoTracking()
            .Where(s => s.RfqId == command.RfqId && vendorIds.Contains(s.VendorId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var vendorId in vendorIds)
        {
            var invitation = rfq.Invitations.FirstOrDefault(i => i.VendorId == vendorId);
            bool submitted = invitation?.Status == RfqInvitationStatus.BidSubmitted;
            var committee = TechnicalEvaluation.Committee([.. scores.Where(s => s.VendorId == vendorId)]);
            bool pass = TechnicalEvaluation.Pass(committee);

            if (!AwardEligibility.IsEligible(rfq, submitted, pass))
            {
                throw new SourcingRuleException($"Vendor {vendorId} is not eligible for an award on this RFQ.");
            }
        }

        var allocations = command.Allocations
            .Select(a =>
            {
                // Stamp unit price from the vendor's submitted bid — never trust a client-typed price.
                var rfqLine = rfq.Lines.FirstOrDefault(l => l.LineCode == a.RfqLineCode)
                    ?? throw new SourcingRuleException($"RFQ line {a.RfqLineCode} does not exist.");
                if (!bidsByVendor.TryGetValue(a.VendorId, out var bid))
                {
                    throw new SourcingRuleException("Cannot allocate to a vendor with no bid on this RFQ.");
                }

                var offered = bid.Lines.FirstOrDefault(l => l.ItemCode == rfqLine.ItemCode && l.Bidding && l.Price > 0)
                    ?? throw new SourcingRuleException($"Vendor did not bid on line {a.RfqLineCode}.");
                return new AwardAllocation(a.RfqLineCode, a.VendorId, a.Qty, offered.Price);
            })
            .ToList();
        AwardAllocationValidator.Validate(rfq, bidsByVendor, allocations);

        string userId = currentUser.GetUserId().ToString();

        var award = await dbContext.Awards
            .FirstOrDefaultAsync(a => a.RfqId == command.RfqId, cancellationToken)
            .ConfigureAwait(false);

        if (award is null)
        {
            string code = await codeGenerator.NextAwardCodeAsync(cancellationToken).ConfigureAwait(false);
            award = Award.Create(code, command.RfqId, userId, allocations);
            dbContext.Awards.Add(award);
        }
        else
        {
            award.MarkPendingApproval(allocations);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return AwardDtoMapper.ToDto(award);
    }
}
