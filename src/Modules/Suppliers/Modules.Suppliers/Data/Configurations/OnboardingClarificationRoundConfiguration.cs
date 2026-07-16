using FSH.Modules.Suppliers.Domain.Onboarding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Suppliers.Data.Configurations;

public sealed class OnboardingClarificationRoundConfiguration : IEntityTypeConfiguration<OnboardingClarificationRound>
{
    public void Configure(EntityTypeBuilder<OnboardingClarificationRound> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("OnboardingClarificationRounds");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Direction).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Message).HasMaxLength(4000);
        builder.Property(x => x.RaisedByUserId).HasMaxLength(100);
        builder.Property(x => x.RaisedByName).HasMaxLength(200);
        builder.HasIndex(x => x.ApplicationId);
        builder.HasIndex(x => new { x.ApplicationId, x.RoundNo });

        builder.OwnsMany(x => x.Items, o =>
        {
            o.ToTable("OnboardingClarificationItems");
            o.WithOwner().HasForeignKey("RoundId");
            o.HasKey(i => i.Id);
            o.Property(i => i.Topic).HasMaxLength(300);
            o.Property(i => i.Request).HasMaxLength(2000);
            o.Property(i => i.Response).HasMaxLength(2000);
        });
    }
}
