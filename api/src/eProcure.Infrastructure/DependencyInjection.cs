using eProcure.Application.Abstractions;
using eProcure.Application.Identity;
using eProcure.Application.Suppliers;
using eProcure.Infrastructure.Persistence;
using eProcure.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace eProcure.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connectionString));

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICodeGenerator, CodeGenerator>();
        services.AddScoped<IAuditLog, AuditLogWriter>();
        services.AddSingleton<INetSuiteClient, NetSuiteClientStub>();
        services.AddScoped<IDataSeeder, DevelopmentDataSeeder>();

        services.AddScoped<IVendorService, VendorService>();
        services.AddScoped<Application.Search.ISearchService, SearchService>();
        services.AddScoped<Application.Configuration.ICustomListService, CustomListService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISwecService, SwecService>();
        services.AddScoped<Application.Audit.IAuditQuery, AuditQuery>();
        services.AddScoped<Application.Sourcing.IRequisitionService, RequisitionService>();
        services.AddScoped<Application.Sourcing.IRfqService, RfqService>();
        services.AddScoped<Application.Sourcing.IRfqVendorService, RfqVendorService>();
        services.AddScoped<Application.Sourcing.IFormService, FormService>();
        services.AddScoped<Application.Sourcing.IBidService, BidService>();
        services.AddScoped<Application.Sourcing.IEvaluationService, EvaluationService>();
        services.AddScoped<Application.Sourcing.IAwardService, AwardService>();
        services.AddScoped<Application.Procurement.IPoService, PoService>();
        services.AddScoped<Application.Procurement.IDeliveryService, DeliveryService>();
        services.AddScoped<Application.Procurement.IInvoiceService, InvoiceService>();
        services.AddScoped<Application.Procurement.IStatementService, StatementService>();
        services.AddScoped<Application.Communication.IClarificationService, ClarificationService>();
        services.AddScoped<Application.Files.IFileStore, FileStore>();
        services.AddScoped<Application.Files.IFileAccessPolicy, FileAccessPolicy>();

        // Vendor onboarding (Slice B): email transport + notifier + invitation/magic-link service.
        services.AddScoped<IEmailSender, Email.SmtpEmailSender>();
        services.AddScoped<Application.Onboarding.IOnboardingNotifier, Email.OnboardingNotifier>();
        services.AddScoped<Application.Onboarding.IOnboardingService, OnboardingService>();
        services.AddScoped<Application.Views.ISavedViewService, SavedViewService>();   // saved-views engine (D3)
        services.AddScoped<Application.Dashboards.ISystemMetricService, SystemMetricService>();   // metric layer (D4)
        services.AddScoped<Application.Dashboards.IDashboardStore, DashboardStoreService>();      // dashboards (D4)
        services.AddScoped<Application.CustomFields.ICustomFieldService, CustomFieldService>();   // custom fields (D5)
        services.AddScoped<Application.Segments.ISegmentService, SegmentService>();              // segments (D6)
        services.AddScoped<Application.Segments.ISegmentProjection, SegmentProjection>();        // the (iii-a) projection
        services.AddScoped<Application.Forms.IEntryFormService, EntryFormService>();             // entry forms (D7)
        services.AddScoped<Application.Forms.IEntryFormSubmitGuard, EntryFormService>();         // the submit-time re-resolution seam
        services.AddScoped<Application.Forms.INumberingService, NumberingService>();             // numbering config (D7)

        return services;
    }
}
