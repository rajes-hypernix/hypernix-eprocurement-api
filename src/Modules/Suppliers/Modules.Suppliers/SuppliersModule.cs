using Asp.Versioning;
using FSH.Framework.Persistence;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Web.Modules;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Features.v1.Onboarding.ApproveOnboardingApplication;
using FSH.Modules.Suppliers.Features.v1.Onboarding.CreateOnboardingInvitation;
using FSH.Modules.Suppliers.Features.v1.Onboarding.DeleteOnboardingDocument;
using FSH.Modules.Suppliers.Features.v1.Onboarding.GetOnboardingApplication;
using FSH.Modules.Suppliers.Features.v1.Onboarding.GetOnboardingDraft;
using FSH.Modules.Suppliers.Features.v1.Onboarding.GetOnboardingLookups;
using FSH.Modules.Suppliers.Features.v1.Onboarding.ListOnboardingApplications;
using FSH.Modules.Suppliers.Features.v1.Onboarding.ListOnboardingInvitations;
using FSH.Modules.Suppliers.Features.v1.Onboarding.RaiseOnboardingClarification;
using FSH.Modules.Suppliers.Features.v1.Onboarding.RejectOnboardingApplication;
using FSH.Modules.Suppliers.Features.v1.Onboarding.RequestOnboardingClarification;
using FSH.Modules.Suppliers.Features.v1.Onboarding.ResendOnboardingInvitation;
using FSH.Modules.Suppliers.Features.v1.Onboarding.ResolveOnboardingLink;
using FSH.Modules.Suppliers.Features.v1.Onboarding.ResubmitOnboardingDraft;
using FSH.Modules.Suppliers.Features.v1.Onboarding.RevokeOnboardingInvitation;
using FSH.Modules.Suppliers.Features.v1.Onboarding.SaveOnboardingDraft;
using FSH.Modules.Suppliers.Features.v1.Onboarding.StartOnboardingReview;
using FSH.Modules.Suppliers.Features.v1.Onboarding.SubmitOnboardingDraft;
using FSH.Modules.Suppliers.Features.v1.Onboarding.UploadOnboardingDocument;
using FSH.Modules.Suppliers.Features.v1.Swec.ListSwecCategories;
using FSH.Modules.Suppliers.Features.v1.VendorUsers.ListVendorUsers;
using FSH.Modules.Suppliers.Features.v1.Vendors.CreateManualVendor;
using FSH.Modules.Suppliers.Features.v1.Vendors.CreateVendor;
using FSH.Modules.Suppliers.Features.v1.Vendors.GetVendorById;
using FSH.Modules.Suppliers.Features.v1.Vendors.SearchVendors;
using FSH.Modules.Suppliers.Features.v1.Vendors.SetVendorCategories;
using FSH.Modules.Suppliers.Features.v1.Vendors.ToggleVendorStatus;
using FSH.Modules.Suppliers.Features.v1.Vendors.UpdateVendor;
using FSH.Modules.Suppliers.Contracts.Services;
using FSH.Modules.Suppliers.Services;
using FSH.Modules.Suppliers.Services.Onboarding;
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
        builder.Services.AddScoped<ISuppliersCodeGenerator, SuppliersCodeGenerator>();
        builder.Services.Configure<OnboardingOptions>(builder.Configuration.GetSection("Onboarding"));
        builder.Services.AddScoped<IOnboardingNotifier, OnboardingNotifier>();
        builder.Services.AddScoped<IVendorPortalUserDirectory, VendorPortalUserDirectory>();
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

        group.MapCreateManualVendorEndpoint();
        group.MapCreateVendorEndpoint();
        group.MapSetVendorCategoriesEndpoint();
        group.MapToggleVendorStatusEndpoint();
        group.MapUpdateVendorEndpoint();
        group.MapListVendorUsersEndpoint();
        group.MapGetVendorByIdEndpoint();
        group.MapSearchVendorsEndpoint();
        group.MapListSwecCategoriesEndpoint();

        group.MapCreateOnboardingInvitationEndpoint();
        group.MapResendOnboardingInvitationEndpoint();
        group.MapRevokeOnboardingInvitationEndpoint();
        group.MapListOnboardingInvitationsEndpoint();
        group.MapStartOnboardingReviewEndpoint();
        group.MapRequestOnboardingClarificationEndpoint();
        group.MapApproveOnboardingApplicationEndpoint();
        group.MapRejectOnboardingApplicationEndpoint();
        group.MapListOnboardingApplicationsEndpoint();
        group.MapGetOnboardingApplicationEndpoint();

        // Vendor-side, anonymous, token-scoped — the magic-link token is the access control, not a bearer token.
        group.MapResolveOnboardingLinkEndpoint();
        group.MapGetOnboardingLookupsEndpoint();
        group.MapGetOnboardingDraftEndpoint();
        group.MapSaveOnboardingDraftEndpoint();
        group.MapSubmitOnboardingDraftEndpoint();
        group.MapUploadOnboardingDocumentEndpoint();
        group.MapDeleteOnboardingDocumentEndpoint();
        group.MapResubmitOnboardingDraftEndpoint();
        group.MapRaiseOnboardingClarificationEndpoint();
    }
}
