using FSH.Modules.Procurement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Procurement.Data.Configurations;

public sealed class PoLineConfiguration : IEntityTypeConfiguration<PoLine>
{
    public void Configure(EntityTypeBuilder<PoLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PoLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ItemCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Uom).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Qty).HasPrecision(18, 4);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.ReceivedQty).HasPrecision(18, 4);
        builder.Property(x => x.InvoicedQty).HasPrecision(18, 4);
        builder.Property(x => x.RfqLineCode).HasMaxLength(50);
        builder.Ignore(x => x.LineTotal);
        builder.Ignore(x => x.DomainEvents);
    }
}
