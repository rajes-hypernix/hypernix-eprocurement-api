using FSH.Modules.Procurement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Procurement.Data.Configurations;

public sealed class AsnConfiguration : IEntityTypeConfiguration<Asn>
{
    public void Configure(EntityTypeBuilder<Asn> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Asns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Carrier).IsRequired().HasMaxLength(200);
        builder.Property(x => x.TrackingNo).IsRequired().HasMaxLength(200);
        builder.Ignore(x => x.DomainEvents);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.AsnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
