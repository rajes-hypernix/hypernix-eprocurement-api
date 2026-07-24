using FSH.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Platform.Data.Configurations;

public sealed class SegmentAssignmentConfiguration : IEntityTypeConfiguration<SegmentAssignment>
{
    public void Configure(EntityTypeBuilder<SegmentAssignment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("SegmentAssignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.RecordType).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Dimension).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.ConfigureAudit();
        builder.Ignore(x => x.DomainEvents);

        // One assignment per dimension per record header; a separate index covers line-level tags
        // (same "two partial indexes" reasoning as CustomFieldValue's LineId nullability).
        builder.HasIndex(x => new { x.RecordType, x.RecordId, x.Dimension })
            .IsUnique()
            .HasFilter("\"LineId\" IS NULL");
        builder.HasIndex(x => new { x.RecordType, x.RecordId, x.LineId, x.Dimension })
            .IsUnique()
            .HasFilter("\"LineId\" IS NOT NULL");
    }
}
