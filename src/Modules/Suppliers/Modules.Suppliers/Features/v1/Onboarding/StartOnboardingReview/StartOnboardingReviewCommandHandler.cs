using FSH.Framework.Core.Exceptions;
using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.StartOnboardingReview;

public sealed class StartOnboardingReviewCommandHandler(SuppliersDbContext dbContext)
    : ICommandHandler<StartOnboardingReviewCommand, OnboardingApplicationDto>
{
    public async ValueTask<OnboardingApplicationDto> Handle(StartOnboardingReviewCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var application = await dbContext.VendorOnboardingApplications
            .FirstOrDefaultAsync(a => a.Id == command.ApplicationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Onboarding application {command.ApplicationId} not found.");

        application.StartReview(DateTime.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return OnboardingDtoMapper.ToDto(application);
    }
}
