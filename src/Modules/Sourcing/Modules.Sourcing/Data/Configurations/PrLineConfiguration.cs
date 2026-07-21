using FSH.Modules.Sourcing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Sourcing.Data.Configurations;

public sealed class PrLineConfiguration : IEntityTypeConfiguration<PrLine>
{
    public void Configure(EntityTypeBuilder<PrLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PrLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ItemCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Uom).HasMaxLength(20);
        builder.Property(x => x.Qty).HasPrecision(18, 4);
        builder.Property(x => x.EstUnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.LifecycleStatus).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Ref).HasMaxLength(50);
        builder.HasIndex(x => x.PurchaseRequisitionId);
        builder.HasIndex(x => x.LifecycleStatus);
    }
}
