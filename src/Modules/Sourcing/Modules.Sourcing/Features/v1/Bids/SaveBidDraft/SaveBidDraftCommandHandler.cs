using FSH.Framework.Core.Context;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Bids;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using FSH.Modules.Sourcing.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Bids.SaveBidDraft;

public sealed class SaveBidDraftCommandHandler(
    SourcingDbContext dbContext,
    ISourcingCodeGenerator codeGenerator,
    ICurrentUser currentUser)
    : ICommandHandler<SaveBidDraftCommand, BidDto>
{
    public async ValueTask<BidDto> Handle(SaveBidDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var vendorId = currentUser.RequireVendorId();
        var now = DateTime.UtcNow;

        var rfq = await BidAuthorization.RequireInvitedRfqAsync(dbContext, command.RfqId, vendorId, cancellationToken).ConfigureAwait(false);
        BidGuards.EnsureOpen(rfq, now);

        var bid = await dbContext.Bids
            .FirstOrDefaultAsync(b => b.RfqId == command.RfqId && b.VendorId == vendorId, cancellationToken)
            .ConfigureAwait(false);

        if (bid is null)
        {
            string code = await codeGenerator.NextBidCodeAsync(cancellationToken).ConfigureAwait(false);
            bid = Bid.CreateDraft(code, command.RfqId, vendorId);
            dbContext.Bids.Add(bid);
        }

        bid.SaveDraft(
            command.Lead,
            command.Warranty,
            [.. command.Lines.Select(l => new BidLine(l.ItemCode, l.Bidding, l.Price, l.Qty, l.Partial, l.AltItem))],
            [.. command.Answers.Select(a => new BidAnswer(a.QuestionOrder, a.Value))],
            [.. command.Files.Select(f => new BidAttachment(f))],
            now);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return BidDtoMapper.ToDto(bid);
    }
}
