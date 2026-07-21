using FSH.Modules.Sourcing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Sourcing.Data.Configurations;

public sealed class AwardConfiguration : IEntityTypeConfiguration<Award>
{
    public void Configure(EntityTypeBuilder<Award> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Awards");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.RfqId).IsUnique();
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.ApproverUserId).HasMaxLength(450);
        builder.Ignore(x => x.TotalValue);

        builder.HasOne<Rfq>().WithMany().HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.Cascade);

        builder.OwnsMany(x => x.Allocations, o =>
        {
            o.ToTable("AwardAllocations");
            o.WithOwner().HasForeignKey("AwardId");
            o.Property<Guid>("Id");
            o.HasKey("Id");
            o.Property(a => a.RfqLineCode).IsRequired().HasMaxLength(50);
            o.Property(a => a.Qty).HasPrecision(18, 4);
            o.Property(a => a.UnitPrice).HasPrecision(18, 4);
        });

        builder.Ignore(x => x.DomainEvents);
    }
}
