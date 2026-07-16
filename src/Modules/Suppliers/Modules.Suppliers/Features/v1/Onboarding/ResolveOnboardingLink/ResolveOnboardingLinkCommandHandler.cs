using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Domain.Onboarding;
using Mediator;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.ResolveOnboardingLink;

public sealed class ResolveOnboardingLinkCommandHandler(SuppliersDbContext dbContext)
    : ICommandHandler<ResolveOnboardingLinkCommand, OnboardingApplicationDto>
{
    public async ValueTask<OnboardingApplicationDto> Handle(ResolveOnboardingLinkCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var (invitation, application) = await OnboardingTokenGate.ResolveAsync(dbContext, command.Token, cancellationToken).ConfigureAwait(false);

        var now = DateTime.UtcNow;
        invitation.Open(command.Token, application.Id, now);
        if (application.Status == OnboardingStatus.Invited)
        {
            application.MarkInProgress(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return OnboardingDtoMapper.ToDto(application);
    }
}
