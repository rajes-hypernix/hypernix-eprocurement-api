using FSH.Modules.Procurement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Procurement.Data.Configurations;

public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PurchaseOrders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(10);
        builder.Ignore(x => x.TotalValue);
        builder.Ignore(x => x.DomainEvents);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
