using System.Text.Json;
using FSH.Modules.Suppliers.Domain.Onboarding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FSH.Modules.Suppliers.Data.Configurations;

public sealed class VendorOnboardingInvitationConfiguration : IEntityTypeConfiguration<VendorOnboardingInvitation>
{
    private static readonly ValueConverter<List<Guid>, string> GuidListConverter = new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>());

    private static readonly ValueComparer<List<Guid>> GuidListComparer = new(
        (a, b) => (a ?? new List<Guid>()).SequenceEqual(b ?? new List<Guid>()),
        v => v.Aggregate(0, HashCode.Combine),
        v => v.ToList());

    public void Configure(EntityTypeBuilder<VendorOnboardingInvitation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("VendorOnboardingInvitations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Type).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);
        builder.Property(x => x.InvitedByUserId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.InvitedByName).IsRequired().HasMaxLength(200);

        builder.Property(x => x.SelectedTemplateIds)
            .HasConversion(GuidListConverter)
            .Metadata.SetValueComparer(GuidListComparer);

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedOnUtc);
        builder.ConfigureAudit();
        builder.HasIndex(x => x.ApplicationId);
        builder.HasIndex(x => x.InvitedByUserId);

        builder.Ignore(x => x.DomainEvents);
    }
}
