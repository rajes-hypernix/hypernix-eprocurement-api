using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using Mediator;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.GetOnboardingDraft;

public sealed class GetOnboardingDraftQueryHandler(SuppliersDbContext dbContext)
    : IQueryHandler<GetOnboardingDraftQuery, OnboardingDraftDto>
{
    public async ValueTask<OnboardingDraftDto> Handle(GetOnboardingDraftQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (_, application) = await OnboardingTokenGate.ResolveAsync(dbContext, query.Token, cancellationToken).ConfigureAwait(false);
        return OnboardingDtoMapper.ToDraftDto(application);
    }
}
