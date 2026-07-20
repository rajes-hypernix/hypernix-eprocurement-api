using FSH.Modules.Sourcing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Sourcing.Data.Configurations;

public sealed class PrLineSourcingConfiguration : IEntityTypeConfiguration<PrLineSourcing>
{
    public void Configure(EntityTypeBuilder<PrLineSourcing> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PrLineSourcings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RfqLineCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.QtySourced).HasPrecision(18, 4);
        builder.Property(x => x.LinkStatus).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.HasIndex(x => x.PrLineId);
        builder.HasIndex(x => x.RfqId);
        builder.HasIndex(x => x.LinkStatus);
    }
}
