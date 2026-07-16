using FSH.Framework.Core.Exceptions;
using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Features.v1.Vendors;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.GetOnboardingApplication;

public sealed class GetOnboardingApplicationQueryHandler(SuppliersDbContext dbContext)
    : IQueryHandler<GetOnboardingApplicationQuery, OnboardingReviewDto>
{
    public async ValueTask<OnboardingReviewDto> Handle(GetOnboardingApplicationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var application = await dbContext.VendorOnboardingApplications
            .AsNoTracking()
            .Include(a => a.Rounds)
            .Include(a => a.Financial)
            .FirstOrDefaultAsync(a => a.Id == query.ApplicationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Onboarding application {query.ApplicationId} not found.");

        string? duplicateWarning = await VendorDuplicateChecker
            .FindWarningAsync(dbContext, application.RegistrationNo, application.Name, cancellationToken)
            .ConfigureAwait(false);

        return OnboardingDtoMapper.ToReviewDto(application, duplicateWarning);
    }
}
