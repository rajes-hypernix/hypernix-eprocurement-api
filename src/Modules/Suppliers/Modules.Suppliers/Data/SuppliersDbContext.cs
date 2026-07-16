using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Persistence.Context;
using FSH.Framework.Shared.Multitenancy;
using FSH.Framework.Shared.Persistence;
using FSH.Modules.Suppliers.Domain;
using FSH.Modules.Suppliers.Domain.Onboarding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Suppliers.Data;

public sealed class SuppliersDbContext : BaseDbContext
{
    public const string Schema = "suppliers";

    public SuppliersDbContext(
        IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
        DbContextOptions<SuppliersDbContext> options,
        IOptions<DatabaseOptions> settings,
        IHostEnvironment environment) : base(multiTenantContextAccessor, options, settings, environment) { }

    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<VendorUser> VendorUsers => Set<VendorUser>();
    public DbSet<SwecCategory> SwecCategories => Set<SwecCategory>();
    public DbSet<VendorOnboardingInvitation> VendorOnboardingInvitations => Set<VendorOnboardingInvitation>();
    public DbSet<VendorOnboardingApplication> VendorOnboardingApplications => Set<VendorOnboardingApplication>();
    public DbSet<VendorFinancialAssessment> VendorFinancialAssessments => Set<VendorFinancialAssessment>();
    public DbSet<OnboardingClarificationRound> OnboardingClarificationRounds => Set<OnboardingClarificationRound>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.HasSequence<long>("VendorCodeSeq", Schema).StartsAt(10001);
        modelBuilder.HasSequence<long>("VendorUserCodeSeq", Schema).StartsAt(1);
        modelBuilder.HasSequence<long>("OnboardingApplicationCodeSeq", Schema).StartsAt(1);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SuppliersDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
