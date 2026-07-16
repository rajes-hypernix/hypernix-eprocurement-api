using Asp.Versioning;
using FSH.Framework.Persistence;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Web.Modules;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Features.v1.Vendors.SearchVendors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

[assembly: FshModule(typeof(FSH.Modules.Suppliers.SuppliersModule), 700)]

namespace FSH.Modules.Suppliers;

public sealed class SuppliersModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        PermissionConstants.Register(SuppliersPermissions.All);
        builder.Services.AddHeroDbContext<SuppliersDbContext>();
        builder.Services.AddScoped<IDbInitializer, SuppliersDbInitializer>();
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<SuppliersDbContext>(
                name: "db:suppliers",
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
            .MapGroup("api/v{version:apiVersion}/suppliers")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization();

        group.MapSearchVendorsEndpoint();
    }
}
