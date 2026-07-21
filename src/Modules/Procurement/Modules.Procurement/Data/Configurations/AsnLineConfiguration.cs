using FSH.Modules.Procurement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Procurement.Data.Configurations;

public sealed class AsnLineConfiguration : IEntityTypeConfiguration<AsnLine>
{
    public void Configure(EntityTypeBuilder<AsnLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("AsnLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ItemCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ShippedQty).HasPrecision(18, 4);
        builder.Property(x => x.LotNo).HasMaxLength(100);
        builder.Ignore(x => x.DomainEvents);
    }
}
