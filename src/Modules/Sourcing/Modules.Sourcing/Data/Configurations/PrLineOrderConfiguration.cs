using FSH.Modules.Sourcing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Sourcing.Data.Configurations;

public sealed class PrLineOrderConfiguration : IEntityTypeConfiguration<PrLineOrder>
{
    public void Configure(EntityTypeBuilder<PrLineOrder> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PrLineOrders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.PoLineRef).HasMaxLength(50);
        builder.Property(x => x.QtyOrdered).HasPrecision(18, 4);
        builder.Property(x => x.LinkStatus).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.HasIndex(x => x.PrLineId);
        builder.HasIndex(x => x.PoId);
        builder.HasIndex(x => x.LinkStatus);
    }
}
