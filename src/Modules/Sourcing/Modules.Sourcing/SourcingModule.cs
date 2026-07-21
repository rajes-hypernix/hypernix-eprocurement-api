using Asp.Versioning;
using FSH.Framework.Persistence;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Web.Modules;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Features.v1.Bids.GetMyBid;
using FSH.Modules.Sourcing.Features.v1.Bids.ListMyInvitations;
using FSH.Modules.Sourcing.Features.v1.Bids.SaveBidDraft;
using FSH.Modules.Sourcing.Features.v1.Bids.SubmitBid;
using FSH.Modules.Sourcing.Features.v1.Bids.WithdrawBid;
using FSH.Modules.Sourcing.Features.v1.Clarifications.GetClarificationThread;
using FSH.Modules.Sourcing.Features.v1.Clarifications.ListClarificationThreads;
using FSH.Modules.Sourcing.Features.v1.Clarifications.SendClarification;
using FSH.Modules.Sourcing.Features.v1.Awards.ApproveAward;
using FSH.Modules.Sourcing.Features.v1.Awards.GetAward;
using FSH.Modules.Sourcing.Features.v1.Awards.GetAwardEligibility;
using FSH.Modules.Sourcing.Features.v1.Awards.ListAwards;
using FSH.Modules.Sourcing.Features.v1.Awards.SubmitAward;
using FSH.Modules.Sourcing.Features.v1.Evaluation.FinalizeTechnical;
using FSH.Modules.Sourcing.Features.v1.Evaluation.GetBidOpening;
using FSH.Modules.Sourcing.Features.v1.Evaluation.GetTechnicalEval;
using FSH.Modules.Sourcing.Features.v1.Evaluation.OpenCommercialEnvelope;
using FSH.Modules.Sourcing.Features.v1.Evaluation.OpenTechnicalEnvelope;
using FSH.Modules.Sourcing.Features.v1.Evaluation.SetScore;
using FSH.Modules.Sourcing.Features.v1.Requisitions.CancelRequisition;
using FSH.Modules.Sourcing.Features.v1.Requisitions.CancelRequisitionLine;
using FSH.Modules.Sourcing.Features.v1.Requisitions.CreateRequisition;
using FSH.Modules.Sourcing.Features.v1.Requisitions.GetRequisitionById;
using FSH.Modules.Sourcing.Features.v1.Requisitions.ListRequisitions;
using FSH.Modules.Sourcing.Features.v1.Requisitions.ReleaseRequisitionLine;
using FSH.Modules.Sourcing.Features.v1.Requisitions.ReopenRequisitionLine;
using FSH.Modules.Sourcing.Features.v1.Requisitions.ReserveRequisitionLine;
using FSH.Modules.Sourcing.Features.v1.Requisitions.SubmitRequisition;
using FSH.Modules.Sourcing.Features.v1.Requisitions.UnreserveRequisitionLine;
using FSH.Modules.Sourcing.Features.v1.Requisitions.UpdateRequisition;
using FSH.Modules.Sourcing.Features.v1.Rfqs.CancelRfq;
using FSH.Modules.Sourcing.Features.v1.Rfqs.CloseRfq;
using FSH.Modules.Sourcing.Features.v1.Rfqs.CreateRfqDraft;
using FSH.Modules.Sourcing.Features.v1.Rfqs.DeclineInvitation;
using FSH.Modules.Sourcing.Features.v1.Rfqs.ExtendRfq;
using FSH.Modules.Sourcing.Features.v1.Rfqs.GetRfqById;
using FSH.Modules.Sourcing.Features.v1.Rfqs.InviteVendor;
using FSH.Modules.Sourcing.Features.v1.Rfqs.ListRfqs;
using FSH.Modules.Sourcing.Features.v1.Rfqs.ReleaseRfq;
using FSH.Modules.Sourcing.Features.v1.Rfqs.RescindInvitation;
using FSH.Modules.Sourcing.Features.v1.Rfqs.UpdateRfqDraft;
using FSH.Modules.Sourcing.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

[assembly: FshModule(typeof(FSH.Modules.Sourcing.SourcingModule), 750)]

namespace FSH.Modules.Sourcing;

public sealed class SourcingModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        PermissionConstants.Register(SourcingPermissions.All);
        builder.Services.AddHeroDbContext<SourcingDbContext>();
        builder.Services.AddScoped<IDbInitializer, SourcingDbInitializer>();
        builder.Services.AddScoped<ISourcingCodeGenerator, SourcingCodeGenerator>();
        builder.Services.Configure<RfqGovernanceOptions>(builder.Configuration.GetSection("RfqGovernance"));
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<SourcingDbContext>(
                name: "db:sourcing",
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
            .MapGroup("api/v{version:apiVersion}/sourcing")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization();

        group.MapCreateRequisitionEndpoint();
        group.MapUpdateRequisitionEndpoint();
        group.MapSubmitRequisitionEndpoint();
        group.MapCancelRequisitionEndpoint();
        group.MapCancelRequisitionLineEndpoint();
        group.MapReleaseRequisitionLineEndpoint();
        group.MapReserveRequisitionLineEndpoint();
        group.MapUnreserveRequisitionLineEndpoint();
        group.MapReopenRequisitionLineEndpoint();
        group.MapListRequisitionsEndpoint();
        group.MapGetRequisitionByIdEndpoint();

        group.MapCreateRfqDraftEndpoint();
        group.MapUpdateRfqDraftEndpoint();
        group.MapInviteVendorEndpoint();
        group.MapRescindInvitationEndpoint();
        group.MapDeclineInvitationEndpoint();
        group.MapExtendRfqEndpoint();
        group.MapReleaseRfqEndpoint();
        group.MapCloseRfqEndpoint();
        group.MapCancelRfqEndpoint();
        group.MapListRfqsEndpoint();
        group.MapGetRfqByIdEndpoint();

        // Vendor-portal, bare RequireAuthorization (no RequirePermission) — scoped by the
        // caller's vendorId claim in-handler, per the Bid module's authorization convention.
        group.MapListMyInvitationsEndpoint();
        group.MapGetMyBidEndpoint();
        group.MapSaveBidDraftEndpoint();
        group.MapSubmitBidEndpoint();
        group.MapWithdrawBidEndpoint();

        group.MapGetBidOpeningEndpoint();
        group.MapOpenTechnicalEnvelopeEndpoint();
        group.MapOpenCommercialEnvelopeEndpoint();
        group.MapGetTechnicalEvalEndpoint();
        group.MapSetScoreEndpoint();
        group.MapFinalizeTechnicalEndpoint();

        group.MapGetAwardEligibilityEndpoint();
        group.MapGetAwardEndpoint();
        group.MapListAwardsEndpoint();
        group.MapSubmitAwardEndpoint();
        group.MapApproveAwardEndpoint();

        group.MapListClarificationThreadsEndpoint();
        group.MapGetClarificationThreadEndpoint();
        group.MapSendClarificationEndpoint();
    }
}
