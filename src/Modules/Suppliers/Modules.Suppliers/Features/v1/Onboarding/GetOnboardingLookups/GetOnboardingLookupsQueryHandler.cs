using FSH.Modules.Platform.Contracts.v1.Catalog;
using FSH.Modules.Platform.Contracts.v1.FormTemplates;
using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.GetOnboardingLookups;

public sealed class GetOnboardingLookupsQueryHandler(
    SuppliersDbContext dbContext,
    IMediator mediator)
    : IQueryHandler<GetOnboardingLookupsQuery, OnboardingLookupsDto>
{
    public async ValueTask<OnboardingLookupsDto> Handle(GetOnboardingLookupsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (_, application) = await OnboardingTokenGate.ResolveAsync(dbContext, query.Token, cancellationToken).ConfigureAwait(false);

        var swec = await dbContext.SwecCategories
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new SwecCategoryDto(c.Code, c.Name, c.ParentCode, c.Level, c.IsLeaf, c.PathText))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var geo = await mediator.Send(new GetGeoCatalogQuery("MY"), cancellationToken).ConfigureAwait(false);
        var templates = application.SelectedTemplateIds.Count == 0
            ? []
            : await mediator.Send(new ListFormTemplatesByIdsQuery(application.SelectedTemplateIds), cancellationToken)
                .ConfigureAwait(false);

        return new OnboardingLookupsDto(swec, geo.Countries, geo.Banks, templates);
    }
}
