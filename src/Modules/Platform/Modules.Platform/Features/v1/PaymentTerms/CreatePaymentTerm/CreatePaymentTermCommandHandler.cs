using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.PaymentTerms.CreatePaymentTerm;

public sealed class CreatePaymentTermCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<CreatePaymentTermCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreatePaymentTermCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = command.Code.Trim().ToUpperInvariant();
        bool exists = await dbContext.PaymentTerms
            .AnyAsync(t => t.Code == code, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
            throw new PlatformRuleException($"Payment term '{code}' already exists.");

        var term = PaymentTerm.Create(
            command.Code,
            command.Name,
            ConfigurationMapping.ParseKind(command.Kind),
            command.DueDays,
            command.DayOfMonth,
            command.MonthsAhead,
            command.MinimumDaysBeforeDue,
            command.DiscountPct,
            command.DiscountDays,
            ConfigurationMapping.ToDrafts(command.Rows));

        dbContext.PaymentTerms.Add(term);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return term.Id;
    }
}
