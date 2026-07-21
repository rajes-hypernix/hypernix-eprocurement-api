using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Persistence.Context;
using FSH.Framework.Shared.Multitenancy;
using FSH.Framework.Shared.Persistence;
using FSH.Modules.Procurement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Procurement.Data;

public sealed class ProcurementDbContext : BaseDbContext
{
    public const string Schema = "procurement";

    public ProcurementDbContext(
        IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
        DbContextOptions<ProcurementDbContext> options,
        IOptions<DatabaseOptions> settings,
        IHostEnvironment environment) : base(multiTenantContextAccessor, options, settings, environment) { }

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PoLine> PoLines => Set<PoLine>();
    public DbSet<Asn> Asns => Set<Asn>();
    public DbSet<AsnLine> AsnLines => Set<AsnLine>();
    public DbSet<Grn> Grns => Set<Grn>();
    public DbSet<GrnLine> GrnLines => Set<GrnLine>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.HasSequence<long>("PoCodeSeq", Schema).StartsAt(1);
        modelBuilder.HasSequence<long>("AsnCodeSeq", Schema).StartsAt(1);
        modelBuilder.HasSequence<long>("GrnCodeSeq", Schema).StartsAt(1);
        modelBuilder.HasSequence<long>("InvCodeSeq", Schema).StartsAt(1);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProcurementDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
