using FSH.Modules.Platform.Contracts.Services;
using FSH.Modules.Sourcing.Data;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Services;

/// <summary>
/// Wave-1 saved-view row source for Sourcing's two list surfaces — Requisition and Rfq. Keys
/// are PascalCase and must match <c>ViewsSeedData</c>'s native field registry rows exactly.
/// </summary>
public sealed class SourcingSavedViewRowSource(SourcingDbContext dbContext, ISavedViewSupplementalDataService supplementalData)
    : ISavedViewRowSource
{
    public Task<IReadOnlyList<IDictionary<string, object?>>> GetRowsAsync(string recordType, CancellationToken ct) =>
        recordType switch
        {
            "Requisition" => GetRequisitionRowsAsync(ct),
            "Rfq" => GetRfqRowsAsync(ct),
            _ => throw new NotSupportedException($"Sourcing does not provide saved-view rows for record type '{recordType}'."),
        };

    private async Task<IReadOnlyList<IDictionary<string, object?>>> GetRequisitionRowsAsync(CancellationToken ct)
    {
        var requisitions = await dbContext.PurchaseRequisitions
            .AsNoTracking()
            .Include(p => p.Lines)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var supplemental = await supplementalData
            .GetSupplementalFieldsAsync("Requisition", [.. requisitions.Select(p => p.Id)], ct)
            .ConfigureAwait(false);

        var rows = new List<IDictionary<string, object?>>(requisitions.Count);
        foreach (var pr in requisitions)
        {
            Dictionary<string, object?> row = new()
            {
                ["Id"] = pr.Id,
                ["Code"] = pr.Code,
                ["Requestor"] = pr.Requestor,
                ["Department"] = pr.Department,
                ["Location"] = pr.Location,
                ["Memo"] = pr.Memo,
                ["Job"] = pr.Job,
                ["Category"] = pr.Category,
                ["CostCentre"] = pr.CostCentre,
                ["Project"] = pr.Project,
                ["RaisedDate"] = pr.RaisedOn,
                ["RequiredDate"] = pr.RequiredOn,
                ["HeaderStatus"] = pr.HeaderStatus.ToString(),
                ["Value"] = pr.Lines.Sum(l => l.Qty * l.EstUnitPrice),
                ["Submitted"] = pr.Submitted,
            };

            if (supplemental.TryGetValue(pr.Id, out var extra))
            {
                foreach (var (key, value) in extra)
                    row[key] = value;
            }

            rows.Add(row);
        }

        return rows;
    }

    private async Task<IReadOnlyList<IDictionary<string, object?>>> GetRfqRowsAsync(CancellationToken ct)
    {
        var rfqs = await dbContext.Rfqs
            .AsNoTracking()
            .Include(r => r.Invitations)
            .Include(r => r.Lines)
            .Include(r => r.FormItems)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var rfqIds = rfqs.Select(r => r.Id).ToList();
        var bidCountsByRfq = await dbContext.Bids
            .AsNoTracking()
            .Where(b => rfqIds.Contains(b.RfqId))
            .GroupBy(b => b.RfqId)
            .Select(g => new { RfqId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RfqId, x => x.Count, ct)
            .ConfigureAwait(false);

        // Rfq is not a PlatformRecordType (no custom fields/segments for it) — no supplemental merge.
        return [.. rfqs.Select(r => (IDictionary<string, object?>)new Dictionary<string, object?>
        {
            ["Id"] = r.Id,
            ["Code"] = r.Code,
            ["Title"] = r.Title,
            ["Envelope"] = r.Envelope.ToString(),
            ["Status"] = r.Status.ToString(),
            ["Currency"] = r.Currency,
            ["ClosesUtc"] = r.ClosesUtc,
            ["InvitedCount"] = r.Invitations.Count,
            ["LineCount"] = r.Lines.Count,
            ["QuestionCount"] = r.FormItems.Count,
            ["BidCount"] = bidCountsByRfq.TryGetValue(r.Id, out var count) ? count : 0,
            ["OwnerUserId"] = r.OwnerUserId,
        })];
    }
}
