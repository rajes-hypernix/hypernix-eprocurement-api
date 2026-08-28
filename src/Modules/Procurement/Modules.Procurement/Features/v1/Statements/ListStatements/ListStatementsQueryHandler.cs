using FSH.Framework.Core.Context;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Statements;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Features.v1.Statements.Internal;
using FSH.Modules.Procurement.Services;
using FSH.Modules.Suppliers.Contracts.Services;
using Mediator;

namespace FSH.Modules.Procurement.Features.v1.Statements.ListStatements;

public sealed class ListStatementsQueryHandler(
    ProcurementDbContext dbContext,
    IVendorLookupService vendorLookup,
    ICurrentUser currentUser)
    : IQueryHandler<ListStatementsQuery, IReadOnlyList<StatementSummaryDto>>
{
    public async ValueTask<IReadOnlyList<StatementSummaryDto>> Handle(
        ListStatementsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var calc = await StatementDataLoader
            .LoadAsync(dbContext, vendorLookup, cancellationToken)
            .ConfigureAwait(false);

        IEnumerable<Guid> ids = calc.VendorIds;
        if (currentUser.GetVendorId() is { } vendorId)
        {
            ids = ids.Where(id => id == vendorId);
        }

        return ids
            .Select(calc.ToSummary)
            .OrderByDescending(s => s.Balance + s.Grni)
            .ToList();
    }
}
