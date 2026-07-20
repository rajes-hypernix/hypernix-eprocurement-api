using FSH.Modules.Sourcing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Sourcing.Data.Configurations;

public sealed class RfqInvitationConfiguration : IEntityTypeConfiguration<RfqInvitation>
{
    public void Configure(EntityTypeBuilder<RfqInvitation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("RfqInvitations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.DeclineReasonCode).HasMaxLength(50);
        builder.Property(x => x.DeclineNote).HasMaxLength(500);
        builder.Property(x => x.RescindReasonCode).HasMaxLength(50);
        builder.Property(x => x.RescindNote).HasMaxLength(500);
        builder.HasIndex(x => new { x.RfqId, x.VendorId }).IsUnique();
        builder.HasIndex(x => x.VendorId);
        builder.HasIndex(x => x.Status);
    }
}
