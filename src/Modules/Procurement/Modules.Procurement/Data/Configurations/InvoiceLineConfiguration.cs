using FSH.Modules.Procurement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Procurement.Data.Configurations;

public sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("InvoiceLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ItemCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Qty).HasPrecision(18, 4);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Ignore(x => x.LineTotal);
        builder.Ignore(x => x.DomainEvents);
    }
}
