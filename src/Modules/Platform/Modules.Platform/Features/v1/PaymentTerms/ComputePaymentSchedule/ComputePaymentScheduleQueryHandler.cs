using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.PaymentTerms.ComputePaymentSchedule;

public sealed class ComputePaymentScheduleQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ComputePaymentScheduleQuery, IReadOnlyList<ScheduleInstalmentDto>>
{
    public async ValueTask<IReadOnlyList<ScheduleInstalmentDto>> Handle(
        ComputePaymentScheduleQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var term = await dbContext.PaymentTerms.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == query.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Payment term {query.Id} not found.");

        var schedule = PaymentTermEngine.ComputeSchedule(term, query.BaseDate);
        return [.. schedule.Select(ConfigurationMapping.ToDto)];
    }
}
