using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.PaymentTerms.GetPaymentTerm;

public sealed class GetPaymentTermQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<GetPaymentTermQuery, PaymentTermDto>
{
    public async ValueTask<PaymentTermDto> Handle(GetPaymentTermQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var term = await dbContext.PaymentTerms.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == query.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Payment term {query.Id} not found.");

        return ConfigurationMapping.ToDto(term);
    }
}
