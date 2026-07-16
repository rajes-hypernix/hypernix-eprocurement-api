using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using Mediator;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.SubmitOnboardingDraft;

public sealed class SubmitOnboardingDraftCommandHandler(SuppliersDbContext dbContext)
    : ICommandHandler<SubmitOnboardingDraftCommand, OnboardingDraftDto>
{
    public async ValueTask<OnboardingDraftDto> Handle(SubmitOnboardingDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var (_, application) = await OnboardingTokenGate.ResolveAsync(dbContext, command.Token, cancellationToken).ConfigureAwait(false);

        application.Submit(DateTime.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return OnboardingDtoMapper.ToDraftDto(application);
    }
}
