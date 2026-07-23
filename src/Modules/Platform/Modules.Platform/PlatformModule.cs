using Asp.Versioning;
using FSH.Framework.Persistence;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Web.Modules;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.Services;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Features.v1.Banks.CreateBank;
using FSH.Modules.Platform.Features.v1.Banks.DeleteBank;
using FSH.Modules.Platform.Features.v1.Banks.ListBanks;
using FSH.Modules.Platform.Features.v1.Banks.SetBankActive;
using FSH.Modules.Platform.Features.v1.Banks.UpdateBank;
using FSH.Modules.Platform.Features.v1.Catalog.GetGeoCatalog;
using FSH.Modules.Platform.Features.v1.Cities.CreateCity;
using FSH.Modules.Platform.Features.v1.Cities.DeleteCity;
using FSH.Modules.Platform.Features.v1.Cities.ListCities;
using FSH.Modules.Platform.Features.v1.Cities.SetCityActive;
using FSH.Modules.Platform.Features.v1.Cities.UpdateCity;
using FSH.Modules.Platform.Features.v1.Countries.CreateCountry;
using FSH.Modules.Platform.Features.v1.Countries.DeleteCountry;
using FSH.Modules.Platform.Features.v1.Countries.ListCountries;
using FSH.Modules.Platform.Features.v1.Countries.SetCountryActive;
using FSH.Modules.Platform.Features.v1.Countries.UpdateCountry;
using FSH.Modules.Platform.Features.v1.CustomLists.CreateCustomList;
using FSH.Modules.Platform.Features.v1.CustomLists.ListCustomListItems;
using FSH.Modules.Platform.Features.v1.CustomLists.ListCustomLists;
using FSH.Modules.Platform.Features.v1.CustomLists.SetCustomListActive;
using FSH.Modules.Platform.Features.v1.CustomLists.UpdateCustomList;
using FSH.Modules.Platform.Features.v1.CustomLists.UpsertCustomListItem;
using FSH.Modules.Platform.Features.v1.FormTemplates.CreateFormTemplate;
using FSH.Modules.Platform.Features.v1.FormTemplates.GetFormTemplate;
using FSH.Modules.Platform.Features.v1.FormTemplates.ListFormTemplates;
using FSH.Modules.Platform.Features.v1.FormTemplates.SetFormTemplateActive;
using FSH.Modules.Platform.Features.v1.OrgUnits.CreateOrgUnit;
using FSH.Modules.Platform.Features.v1.OrgUnits.GetOrgCatalog;
using FSH.Modules.Platform.Features.v1.OrgUnits.ListOrgUnits;
using FSH.Modules.Platform.Features.v1.OrgUnits.SetOrgUnitActive;
using FSH.Modules.Platform.Features.v1.States.CreateState;
using FSH.Modules.Platform.Features.v1.States.DeleteState;
using FSH.Modules.Platform.Features.v1.States.ListStates;
using FSH.Modules.Platform.Features.v1.States.SetStateActive;
using FSH.Modules.Platform.Features.v1.States.UpdateState;
using FSH.Modules.Platform.Features.v1.Views.CreateSavedView;
using FSH.Modules.Platform.Features.v1.Views.DeleteSavedView;
using FSH.Modules.Platform.Features.v1.Views.GetViewFields;
using FSH.Modules.Platform.Features.v1.Views.ListSavedViews;
using FSH.Modules.Platform.Features.v1.Views.RunSavedView;
using FSH.Modules.Platform.Features.v1.Views.ShareSavedView;
using FSH.Modules.Platform.Features.v1.Views.UpdateSavedView;
using FSH.Modules.Platform.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

[assembly: FshModule(typeof(FSH.Modules.Platform.PlatformModule), 650)]

namespace FSH.Modules.Platform;

public sealed class PlatformModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        PermissionConstants.Register(PlatformPermissions.All);
        builder.Services.AddHeroDbContext<PlatformDbContext>();
        builder.Services.AddScoped<IDbInitializer, PlatformDbInitializer>();
        builder.Services.AddScoped<IReasonCodeValidator, ReasonCodeValidator>();
        builder.Services.AddScoped<IFormTemplateCatalog, FormTemplateCatalog>();
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<PlatformDbContext>(
                name: "db:platform",
                failureStatus: HealthStatus.Unhealthy);
    }

    public void ConfigureMiddleware(IApplicationBuilder app) { }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var versionSet = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = endpoints
            .MapGroup("api/v{version:apiVersion}/platform")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization();

        group.MapListCountriesEndpoint();
        group.MapCreateCountryEndpoint();
        group.MapUpdateCountryEndpoint();
        group.MapSetCountryActiveEndpoint();
        group.MapDeleteCountryEndpoint();
        group.MapListStatesEndpoint();
        group.MapCreateStateEndpoint();
        group.MapUpdateStateEndpoint();
        group.MapSetStateActiveEndpoint();
        group.MapDeleteStateEndpoint();
        group.MapListCitiesEndpoint();
        group.MapCreateCityEndpoint();
        group.MapUpdateCityEndpoint();
        group.MapSetCityActiveEndpoint();
        group.MapDeleteCityEndpoint();
        group.MapListBanksEndpoint();
        group.MapCreateBankEndpoint();
        group.MapUpdateBankEndpoint();
        group.MapSetBankActiveEndpoint();
        group.MapDeleteBankEndpoint();
        group.MapGetGeoCatalogEndpoint();

        group.MapListCustomListsEndpoint();
        group.MapListCustomListItemsEndpoint();
        group.MapCreateCustomListEndpoint();
        group.MapUpdateCustomListEndpoint();
        group.MapSetCustomListActiveEndpoint();
        group.MapUpsertCustomListItemEndpoint();

        group.MapListOrgUnitsEndpoint();
        group.MapGetOrgCatalogEndpoint();
        group.MapCreateOrgUnitEndpoint();
        group.MapSetOrgUnitActiveEndpoint();

        group.MapListFormTemplatesEndpoint();
        group.MapGetFormTemplateEndpoint();
        group.MapCreateFormTemplateEndpoint();
        group.MapSetFormTemplateActiveEndpoint();

        group.MapListSavedViewsEndpoint();
        group.MapGetViewFieldsEndpoint();
        group.MapCreateSavedViewEndpoint();
        group.MapUpdateSavedViewEndpoint();
        group.MapDeleteSavedViewEndpoint();
        group.MapShareSavedViewEndpoint();
        group.MapRunSavedViewEndpoint();
    }
}
