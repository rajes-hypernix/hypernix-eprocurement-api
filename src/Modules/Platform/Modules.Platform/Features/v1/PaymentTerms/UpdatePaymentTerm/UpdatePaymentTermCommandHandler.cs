using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.PaymentTerms.UpdatePaymentTerm;

public sealed class UpdatePaymentTermCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<UpdatePaymentTermCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdatePaymentTermCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var term = await dbContext.PaymentTerms
            .FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Payment term {command.Id} not found.");

        term.Update(
            command.Name,
            ConfigurationMapping.ParseKind(command.Kind),
            command.DueDays,
            command.DayOfMonth,
            command.MonthsAhead,
            command.MinimumDaysBeforeDue,
            command.DiscountPct,
            command.DiscountDays,
            ConfigurationMapping.ToDrafts(command.Rows));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return term.Id;
    }
}
