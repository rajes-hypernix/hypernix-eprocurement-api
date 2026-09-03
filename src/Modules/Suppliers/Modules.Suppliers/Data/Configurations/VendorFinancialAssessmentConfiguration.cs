using FSH.Modules.Suppliers.Domain.Onboarding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Suppliers.Data.Configurations;

public sealed class VendorFinancialAssessmentConfiguration : IEntityTypeConfiguration<VendorFinancialAssessment>
{
    public void Configure(EntityTypeBuilder<VendorFinancialAssessment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("VendorFinancialAssessments");
        builder.HasKey(x => x.Id);
        // App-assigned Guid.CreateVersion7. Without this, EF treats the new assessment as Unchanged
        // and inserts OnboardingFinancialYears with a missing AssessmentId parent (Postgres 23503).
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.ApplicationId).IsUnique();
        builder.Property(x => x.Remarks).HasMaxLength(4000);
        builder.ConfigureAudit();
        builder.Ignore(x => x.LiveWeightedZ);

        builder.OwnsMany(x => x.Years, o =>
        {
            o.ToTable("OnboardingFinancialYears");
            o.WithOwner().HasForeignKey("AssessmentId");
            o.Property<Guid>("Id");
            o.HasKey("Id");
            o.HasIndex(y => y.YearIndex);
        });
        builder.Navigation(x => x.Years).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.Snapshots, o =>
        {
            o.ToTable("OnboardingFinancialSnapshots");
            o.WithOwner().HasForeignKey("AssessmentId");
            o.HasKey(s => s.Id);
            o.Property(s => s.Stage).IsRequired().HasConversion<string>().HasMaxLength(20);
            o.Property(s => s.Band).IsRequired().HasConversion<string>().HasMaxLength(5);
            o.Property(s => s.Risk).IsRequired().HasConversion<string>().HasMaxLength(20);
            o.Property(s => s.Statement).HasMaxLength(1000);
        });
        builder.Navigation(x => x.Snapshots).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(x => x.DomainEvents);
    }
}
