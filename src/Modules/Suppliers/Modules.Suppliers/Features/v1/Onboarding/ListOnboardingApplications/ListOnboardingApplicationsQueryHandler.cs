using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Domain.Onboarding;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.ListOnboardingApplications;

public sealed class ListOnboardingApplicationsQueryHandler(SuppliersDbContext dbContext)
    : IQueryHandler<ListOnboardingApplicationsQuery, IReadOnlyList<OnboardingQueueItemDto>>
{
    public async ValueTask<IReadOnlyList<OnboardingQueueItemDto>> Handle(ListOnboardingApplicationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var applications = await dbContext.VendorOnboardingApplications
            .AsNoTracking()
            .Include(a => a.Rounds)
            .Where(a => a.Status != OnboardingStatus.Draft)
            .OrderByDescending(a => a.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. applications.Select(OnboardingDtoMapper.ToQueueItemDto)];
    }
}
