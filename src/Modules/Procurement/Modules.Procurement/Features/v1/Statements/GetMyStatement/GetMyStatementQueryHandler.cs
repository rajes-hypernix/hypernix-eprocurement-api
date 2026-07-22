using FSH.Framework.Core.Context;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Statements;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Features.v1.Statements.Internal;
using FSH.Modules.Procurement.Services;
using Mediator;

namespace FSH.Modules.Procurement.Features.v1.Statements.GetMyStatement;

public sealed class GetMyStatementQueryHandler(ProcurementDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<GetMyStatementQuery, StatementDetailDto?>
{
    public async ValueTask<StatementDetailDto?> Handle(GetMyStatementQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var vendorId = currentUser.RequireVendorId();
        var calc = await StatementDataLoader.LoadAsync(dbContext, cancellationToken).ConfigureAwait(false);

        return calc.VendorIds.Contains(vendorId)
            ? calc.ToDetail(vendorId)
            : new StatementDetailDto(vendorId, SoaCalculator.VendorName(vendorId), 0, 0, 0, 0, new AgingDto(0, 0, 0, 0), []);
    }
}
