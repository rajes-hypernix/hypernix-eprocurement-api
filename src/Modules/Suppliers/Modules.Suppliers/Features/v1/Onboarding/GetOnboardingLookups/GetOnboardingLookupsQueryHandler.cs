using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.GetOnboardingLookups;

/// <summary>
/// SWEC taxonomy only for now — Country/State/City/Bank custom-list lookups are deferred until a
/// Platform/Configuration module exists.
/// </summary>
public sealed class GetOnboardingLookupsQueryHandler(SuppliersDbContext dbContext)
    : IQueryHandler<GetOnboardingLookupsQuery, OnboardingLookupsDto>
{
    public async ValueTask<OnboardingLookupsDto> Handle(GetOnboardingLookupsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        await OnboardingTokenGate.ResolveAsync(dbContext, query.Token, cancellationToken).ConfigureAwait(false);

        var swec = await dbContext.SwecCategories
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new SwecCategoryDto(c.Code, c.Name, c.ParentCode, c.Level, c.IsLeaf, c.PathText))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new OnboardingLookupsDto(swec);
    }
}
