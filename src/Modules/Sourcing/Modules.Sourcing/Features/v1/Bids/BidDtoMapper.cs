using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Domain;

namespace FSH.Modules.Sourcing.Features.v1.Bids;

internal static class BidDtoMapper
{
    internal static BidDto ToDto(Bid bid)
    {
        ArgumentNullException.ThrowIfNull(bid);
        return new BidDto(
            bid.Id,
            bid.Code,
            bid.RfqId,
            bid.VendorId,
            bid.Submitted,
            bid.SubmittedUtc,
            bid.SavedDraft,
            bid.WithdrawnUtc,
            bid.Lead,
            bid.Warranty,
            [.. bid.Lines.Select(l => new BidLineDto(l.ItemCode, l.Bidding, l.Price, l.Qty, l.Partial, l.AltItem))],
            [.. bid.Answers.Select(a => new BidAnswerDto(a.QuestionOrder, a.Value))],
            [.. bid.Files.Select(f => f.FileName)]);
    }
}
