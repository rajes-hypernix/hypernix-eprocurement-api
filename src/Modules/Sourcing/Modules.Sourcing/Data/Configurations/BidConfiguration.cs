using FSH.Modules.Sourcing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Sourcing.Data.Configurations;

public sealed class BidConfiguration : IEntityTypeConfiguration<Bid>
{
    public void Configure(EntityTypeBuilder<Bid> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Bids");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => new { x.RfqId, x.VendorId }).IsUnique();
        builder.Property(x => x.Lead).HasMaxLength(500);
        builder.Property(x => x.Warranty).HasMaxLength(500);

        builder.HasOne<Rfq>().WithMany().HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.Cascade);

        builder.OwnsMany(x => x.Lines, o =>
        {
            o.ToTable("BidLines");
            o.WithOwner().HasForeignKey("BidId");
            o.Property<Guid>("Id");
            o.HasKey("Id");
            o.Property(l => l.ItemCode).IsRequired().HasMaxLength(50);
            o.Property(l => l.Price).HasPrecision(18, 4);
            o.Property(l => l.Qty).HasPrecision(18, 4);
            o.Property(l => l.AltItem).HasMaxLength(50);
        });

        builder.OwnsMany(x => x.Answers, o =>
        {
            o.ToTable("BidAnswers");
            o.WithOwner().HasForeignKey("BidId");
            o.Property<Guid>("Id");
            o.HasKey("Id");
            o.Property(a => a.Value).IsRequired().HasMaxLength(2000);
        });

        builder.OwnsMany(x => x.Files, o =>
        {
            o.ToTable("BidAttachments");
            o.WithOwner().HasForeignKey("BidId");
            o.Property<Guid>("Id");
            o.HasKey("Id");
            o.Property(f => f.FileName).IsRequired().HasMaxLength(300);
        });

        builder.Ignore(x => x.DomainEvents);
    }
}
