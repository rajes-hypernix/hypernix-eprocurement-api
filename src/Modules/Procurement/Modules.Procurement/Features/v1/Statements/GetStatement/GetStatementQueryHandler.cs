using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Statements;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Features.v1.Statements.Internal;
using FSH.Modules.Procurement.Services;
using Mediator;

namespace FSH.Modules.Procurement.Features.v1.Statements.GetStatement;

public sealed class GetStatementQueryHandler(ProcurementDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<GetStatementQuery, StatementDetailDto?>
{
    public async ValueTask<StatementDetailDto?> Handle(GetStatementQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (currentUser.GetVendorId() is { } callerVendorId && callerVendorId != query.VendorId)
        {
            throw new ForbiddenException("You may only view your own statement.");
        }

        var calc = await StatementDataLoader.LoadAsync(dbContext, cancellationToken).ConfigureAwait(false);
        return calc.VendorIds.Contains(query.VendorId) ? calc.ToDetail(query.VendorId) : null;
    }
}
