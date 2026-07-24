using Asp.Versioning;
using FSH.Framework.Persistence;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Web.Modules;
using FSH.Modules.Platform.Contracts.Services;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Features.v1.Asns.CreateAsn;
using FSH.Modules.Procurement.Features.v1.Asns.GetAsn;
using FSH.Modules.Procurement.Features.v1.Asns.ListAsns;
using FSH.Modules.Procurement.Features.v1.Grns.GetGrnByAsn;
using FSH.Modules.Procurement.Features.v1.Grns.ReceiveAsn;
using FSH.Modules.Procurement.Features.v1.Invoices.ApproveInvoice;
using FSH.Modules.Procurement.Features.v1.Invoices.GetInvoice;
using FSH.Modules.Procurement.Features.v1.Invoices.ListInvoices;
using FSH.Modules.Procurement.Features.v1.Invoices.ResolveInvoiceException;
using FSH.Modules.Procurement.Features.v1.Invoices.SubmitInvoice;
using FSH.Modules.Procurement.Features.v1.PurchaseOrders.AcknowledgePurchaseOrder;
using FSH.Modules.Procurement.Features.v1.PurchaseOrders.CancelPurchaseOrder;
using FSH.Modules.Procurement.Features.v1.PurchaseOrders.ClosePurchaseOrder;
using FSH.Modules.Procurement.Features.v1.PurchaseOrders.CreateFromAward;
using FSH.Modules.Procurement.Features.v1.PurchaseOrders.CreateFromRequisition;
using FSH.Modules.Procurement.Features.v1.PurchaseOrders.CreateStandalone;
using FSH.Modules.Procurement.Features.v1.PurchaseOrders.GetPurchaseOrder;
using FSH.Modules.Procurement.Features.v1.PurchaseOrders.IssuePurchaseOrder;
using FSH.Modules.Procurement.Features.v1.PurchaseOrders.ListPurchaseOrders;
using FSH.Modules.Procurement.Features.v1.PurchaseOrders.ReopenPurchaseOrderDraft;
using FSH.Modules.Procurement.Features.v1.PurchaseOrders.SetPurchaseOrderShipTo;
using FSH.Modules.Procurement.Features.v1.PurchaseOrders.UpdatePurchaseOrderLine;
using FSH.Modules.Procurement.Features.v1.PurchaseOrders.VerifyPurchaseOrder;
using FSH.Modules.Procurement.Features.v1.Statements.GetMyStatement;
using FSH.Modules.Procurement.Features.v1.Statements.GetStatement;
using FSH.Modules.Procurement.Features.v1.Statements.ListStatements;
using FSH.Modules.Procurement.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

[assembly: FshModule(typeof(FSH.Modules.Procurement.ProcurementModule), 800)]

namespace FSH.Modules.Procurement;

public sealed class ProcurementModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        PermissionConstants.Register(ProcurementPermissions.All);
        builder.Services.AddHeroDbContext<ProcurementDbContext>();
        builder.Services.AddScoped<IDbInitializer, ProcurementDbInitializer>();
        builder.Services.AddScoped<IProcurementCodeGenerator, ProcurementCodeGenerator>();
        builder.Services.AddScoped<ISavedViewRowSource, ProcurementSavedViewRowSource>();
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<ProcurementDbContext>(
                name: "db:procurement",
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
            .MapGroup("api/v{version:apiVersion}/procurement")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization();

        group.MapCreatePurchaseOrdersFromAwardEndpoint();
        group.MapCreatePurchaseOrderFromRequisitionEndpoint();
        group.MapCreateStandalonePurchaseOrderEndpoint();
        group.MapGetPurchaseOrderEndpoint();
        group.MapListPurchaseOrdersEndpoint();
        group.MapVerifyPurchaseOrderEndpoint();
        group.MapReopenPurchaseOrderDraftEndpoint();
        group.MapIssuePurchaseOrderEndpoint();
        group.MapAcknowledgePurchaseOrderEndpoint();
        group.MapCancelPurchaseOrderEndpoint();
        group.MapClosePurchaseOrderEndpoint();
        group.MapSetPurchaseOrderShipToEndpoint();
        group.MapUpdatePurchaseOrderLineEndpoint();

        group.MapCreateAsnEndpoint();
        group.MapListAsnsEndpoint();
        group.MapGetAsnEndpoint();

        group.MapReceiveAsnEndpoint();
        group.MapGetGrnByAsnEndpoint();

        group.MapSubmitInvoiceEndpoint();
        group.MapGetInvoiceEndpoint();
        group.MapListInvoicesEndpoint();
        group.MapApproveInvoiceEndpoint();
        group.MapResolveInvoiceExceptionEndpoint();

        group.MapListStatementsEndpoint();
        group.MapGetMyStatementEndpoint();
        group.MapGetStatementEndpoint();
    }
}
