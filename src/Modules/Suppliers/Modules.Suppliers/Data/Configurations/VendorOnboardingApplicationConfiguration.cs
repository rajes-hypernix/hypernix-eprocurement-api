using System.Text.Json;
using FSH.Modules.Suppliers.Domain.Onboarding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FSH.Modules.Suppliers.Data.Configurations;

public sealed class VendorOnboardingApplicationConfiguration : IEntityTypeConfiguration<VendorOnboardingApplication>
{
    private static readonly ValueConverter<List<string>, string> StringListConverter = new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

    private static readonly ValueComparer<List<string>> StringListComparer = new(
        (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
        v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode(StringComparison.Ordinal))),
        v => v.ToList());

    private static readonly ValueConverter<List<Guid>, string> GuidListConverter = new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>());

    private static readonly ValueComparer<List<Guid>> GuidListComparer = new(
        (a, b) => (a ?? new List<Guid>()).SequenceEqual(b ?? new List<Guid>()),
        v => v.Aggregate(0, HashCode.Combine),
        v => v.ToList());

    public void Configure(EntityTypeBuilder<VendorOnboardingApplication> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("VendorOnboardingApplications");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Source).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Type).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.RegisteredName).HasMaxLength(200);
        builder.Property(x => x.RegistrationNo).HasMaxLength(60);
        builder.Property(x => x.TaxId).HasMaxLength(60);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.ContactName).HasMaxLength(200);
        builder.Property(x => x.ContactPhone).HasMaxLength(50);
        builder.Property(x => x.Region).HasMaxLength(50);
        builder.Property(x => x.State).HasMaxLength(100);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.Country).HasMaxLength(100);
        builder.Property(x => x.RejectReason).HasMaxLength(1000);

        builder.Property(x => x.Categories)
            .HasConversion(StringListConverter)
            .Metadata.SetValueComparer(StringListComparer);

        builder.Property(x => x.SelectedTemplateIds)
            .HasConversion(GuidListConverter)
            .Metadata.SetValueComparer(GuidListComparer);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Type);
        builder.HasIndex(x => x.Source);
        builder.HasIndex(x => x.InvitationId);
        builder.HasIndex(x => x.CreatedUtc);
        builder.HasIndex(x => x.SubmittedUtc);
        builder.HasIndex(x => x.DecisionUtc);

        builder.OwnsMany(x => x.Contacts, o =>
        {
            o.ToTable("OnboardingContacts");
            o.WithOwner().HasForeignKey("ApplicationId");
            o.Property<Guid>("Id");
            o.HasKey("Id");
        });

        builder.OwnsMany(x => x.Addresses, o =>
        {
            o.ToTable("OnboardingAddresses");
            o.WithOwner().HasForeignKey("ApplicationId");
            o.Property<Guid>("Id");
            o.HasKey("Id");
        });

        builder.OwnsMany(x => x.BankAccounts, o =>
        {
            o.ToTable("OnboardingBankAccounts");
            o.WithOwner().HasForeignKey("ApplicationId");
            o.Property<Guid>("Id");
            o.HasKey("Id");
        });

        builder.OwnsMany(x => x.Certifications, o =>
        {
            o.ToTable("OnboardingCertifications");
            o.WithOwner().HasForeignKey("ApplicationId");
            o.Property<Guid>("Id");
            o.HasKey("Id");
        });

        builder.OwnsMany(x => x.Answers, o =>
        {
            o.ToTable("OnboardingAnswers");
            o.WithOwner().HasForeignKey("ApplicationId");
            o.Property<Guid>("Id");
            o.HasKey("Id");
            o.Property(a => a.Value).HasMaxLength(4000);
            o.HasIndex(a => a.FormTemplateId);
        });

        builder.OwnsMany(x => x.Documents, o =>
        {
            o.ToTable("OnboardingDocuments");
            o.WithOwner().HasForeignKey("ApplicationId");
            o.Property<Guid>("Id");
            o.HasKey("Id");
            o.Property(d => d.Key).HasMaxLength(40);
            o.Property(d => d.FileName).HasMaxLength(260);
            o.Property(d => d.StorageKey).HasMaxLength(500);
        });

        builder.OwnsMany(x => x.Steps, o =>
        {
            o.ToTable("OnboardingReviewSteps");
            o.WithOwner().HasForeignKey("ApplicationId");
            o.Property<Guid>("Id");
            o.HasKey("Id");
            o.Property(s => s.Name).HasMaxLength(120);
            o.Property(s => s.Outcome).HasMaxLength(20);
            o.Property(s => s.Reason).HasMaxLength(1000);
            o.HasIndex(s => s.DecidedByUserId);
        });

        // Financial assessment (1:1) and clarification rounds (1:many) are separate entities owned by the aggregate.
        builder.HasOne(x => x.Financial).WithOne()
            .HasForeignKey<VendorFinancialAssessment>(f => f.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Rounds).WithOne()
            .HasForeignKey(r => r.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Rounds).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(x => x.DomainEvents);
    }
}
