using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Domain.Onboarding;
using Mediator;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.RaiseOnboardingClarification;

public sealed class RaiseOnboardingClarificationCommandHandler(SuppliersDbContext dbContext)
    : ICommandHandler<RaiseOnboardingClarificationCommand, OnboardingApplicationDto>
{
    public async ValueTask<OnboardingApplicationDto> Handle(RaiseOnboardingClarificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var (_, application) = await OnboardingTokenGate.ResolveAsync(dbContext, command.Token, cancellationToken).ConfigureAwait(false);

        string byName = string.IsNullOrWhiteSpace(application.ContactName) ? application.Name : application.ContactName;
        var items = command.Items.Select(i => new OnboardingClarificationItem(i.Topic, i.Request));
        application.RaiseVendorClarification(command.Message, byName, items, DateTime.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return OnboardingDtoMapper.ToDto(application);
    }
}
