using FSH.Modules.Sourcing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Sourcing.Data.Configurations;

public sealed class ClarificationConfiguration : IEntityTypeConfiguration<Clarification>
{
    public void Configure(EntityTypeBuilder<Clarification> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Clarifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Scope).IsRequired().HasMaxLength(50);
        builder.Property(x => x.SenderKind).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.SenderName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.RecipientUserId).HasMaxLength(450);
        builder.Property(x => x.Body).IsRequired().HasMaxLength(4000);
        builder.HasIndex(x => new { x.Scope, x.VendorId });
        builder.Ignore(x => x.DomainEvents);
    }
}
