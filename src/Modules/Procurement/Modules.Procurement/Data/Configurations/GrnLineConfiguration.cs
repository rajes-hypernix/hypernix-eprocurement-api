using FSH.Modules.Procurement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Procurement.Data.Configurations;

public sealed class GrnLineConfiguration : IEntityTypeConfiguration<GrnLine>
{
    public void Configure(EntityTypeBuilder<GrnLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("GrnLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ItemCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ExpectedQty).HasPrecision(18, 4);
        builder.Property(x => x.ReceivedQty).HasPrecision(18, 4);
        builder.Property(x => x.Condition).IsRequired().HasMaxLength(20);
        builder.Ignore(x => x.DomainEvents);
    }
}
