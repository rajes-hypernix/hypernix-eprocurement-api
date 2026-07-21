using FSH.Modules.Sourcing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Sourcing.Data.Configurations;

public sealed class TechnicalScoreConfiguration : IEntityTypeConfiguration<TechnicalScore>
{
    public void Configure(EntityTypeBuilder<TechnicalScore> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("TechnicalScores");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.EvaluatorId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.Criterion).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => new { x.RfqId, x.VendorId, x.EvaluatorId, x.Criterion }).IsUnique();

        builder.HasOne<Rfq>().WithMany().HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(x => x.DomainEvents);
    }
}
