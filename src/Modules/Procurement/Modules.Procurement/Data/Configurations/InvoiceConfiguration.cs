using FSH.Modules.Procurement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Procurement.Data.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Invoices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.InvoiceNo).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.WhtRate).HasPrecision(18, 4);
        builder.Property(x => x.ExceptionReason).HasMaxLength(1000);
        builder.Ignore(x => x.Subtotal);
        builder.Ignore(x => x.SstAmount);
        builder.Ignore(x => x.WhtAmount);
        builder.Ignore(x => x.Total);
        builder.Ignore(x => x.DomainEvents);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
