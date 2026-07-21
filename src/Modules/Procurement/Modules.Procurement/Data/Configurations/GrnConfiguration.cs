using FSH.Modules.Procurement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Procurement.Data.Configurations;

public sealed class GrnConfiguration : IEntityTypeConfiguration<Grn>
{
    public void Configure(EntityTypeBuilder<Grn> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Grns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.AsnId).IsUnique();
        builder.Ignore(x => x.DomainEvents);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.GrnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
