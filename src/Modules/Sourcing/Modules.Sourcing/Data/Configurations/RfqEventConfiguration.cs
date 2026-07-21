using FSH.Modules.Sourcing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Sourcing.Data.Configurations;

public sealed class RfqEventConfiguration : IEntityTypeConfiguration<RfqEvent>
{
    public void Configure(EntityTypeBuilder<RfqEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("RfqEvents");
        builder.HasKey(x => x.Id);
        // App-assigned Guid v7. Without ValueGeneratedNever, EF tracks new nav children as Modified → UPDATE-0-rows.
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.EventType).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.ActorUserId).HasMaxLength(100);
        builder.Property(x => x.ActorVendorUserId).HasMaxLength(100);
        builder.Property(x => x.ReasonCode).HasMaxLength(50);
        builder.Property(x => x.ReasonNote).HasMaxLength(500);
        builder.HasIndex(x => new { x.RfqId, x.OccurredUtc });
    }
}
