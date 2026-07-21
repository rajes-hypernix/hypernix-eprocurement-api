using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Persistence.Context;
using FSH.Framework.Shared.Multitenancy;
using FSH.Framework.Shared.Persistence;
using FSH.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Platform.Data;

public sealed class PlatformDbContext : BaseDbContext
{
    public const string Schema = "platform";

    public PlatformDbContext(
        IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
        DbContextOptions<PlatformDbContext> options,
        IOptions<DatabaseOptions> settings,
        IHostEnvironment environment) : base(multiTenantContextAccessor, options, settings, environment) { }

    public DbSet<Country> Countries => Set<Country>();
    public DbSet<State> States => Set<State>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<CustomList> CustomLists => Set<CustomList>();
    public DbSet<CustomListItem> CustomListItems => Set<CustomListItem>();
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<FormTemplate> FormTemplates => Set<FormTemplate>();
    public DbSet<FormTemplateQuestion> FormTemplateQuestions => Set<FormTemplateQuestion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
