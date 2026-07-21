using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Persistence.Context;
using FSH.Framework.Shared.Multitenancy;
using FSH.Framework.Shared.Persistence;
using FSH.Modules.Sourcing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Sourcing.Data;

public sealed class SourcingDbContext : BaseDbContext
{
    public const string Schema = "sourcing";

    public SourcingDbContext(
        IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
        DbContextOptions<SourcingDbContext> options,
        IOptions<DatabaseOptions> settings,
        IHostEnvironment environment) : base(multiTenantContextAccessor, options, settings, environment) { }

    public DbSet<PurchaseRequisition> PurchaseRequisitions => Set<PurchaseRequisition>();
    public DbSet<PrLine> PrLines => Set<PrLine>();
    public DbSet<PrLineSourcing> PrLineSourcings => Set<PrLineSourcing>();
    public DbSet<Rfq> Rfqs => Set<Rfq>();
    public DbSet<RfqInvitation> RfqInvitations => Set<RfqInvitation>();
    public DbSet<RfqEvent> RfqEvents => Set<RfqEvent>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<TechnicalScore> TechnicalScores => Set<TechnicalScore>();
    public DbSet<Award> Awards => Set<Award>();
    public DbSet<Clarification> Clarifications => Set<Clarification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.HasSequence<long>("PrCodeSeq", Schema).StartsAt(1);
        modelBuilder.HasSequence<long>("RfqCodeSeq", Schema).StartsAt(1);
        modelBuilder.HasSequence<long>("BidCodeSeq", Schema).StartsAt(1);
        modelBuilder.HasSequence<long>("AwardCodeSeq", Schema).StartsAt(1);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SourcingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
