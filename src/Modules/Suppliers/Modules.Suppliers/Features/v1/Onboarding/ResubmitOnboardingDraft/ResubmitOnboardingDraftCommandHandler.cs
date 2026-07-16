using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using Mediator;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.ResubmitOnboardingDraft;

public sealed class ResubmitOnboardingDraftCommandHandler(SuppliersDbContext dbContext)
    : ICommandHandler<ResubmitOnboardingDraftCommand, OnboardingApplicationDto>
{
    public async ValueTask<OnboardingApplicationDto> Handle(ResubmitOnboardingDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var (_, application) = await OnboardingTokenGate.ResolveAsync(dbContext, command.Token, cancellationToken).ConfigureAwait(false);

        application.Resubmit(command.Responses, DateTime.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return OnboardingDtoMapper.ToDto(application);
    }
}
