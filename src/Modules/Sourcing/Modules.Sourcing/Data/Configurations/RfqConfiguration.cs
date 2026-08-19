using System.Text.Json;
using FSH.Modules.Sourcing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FSH.Modules.Sourcing.Data.Configurations;

public sealed class RfqConfiguration : IEntityTypeConfiguration<Rfq>
{
    private static readonly ValueConverter<List<string>, string> StringListConverter = new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

    private static readonly ValueComparer<List<string>> StringListComparer = new(
        (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
        v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode(StringComparison.Ordinal))),
        v => v.ToList());

    public void Configure(EntityTypeBuilder<Rfq> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Rfqs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Title).HasMaxLength(300);
        builder.Property(x => x.Envelope).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.ExchangeRateToBase).HasPrecision(18, 6);
        builder.Property(x => x.OwnerUserId).HasMaxLength(100);
        builder.Property(x => x.IncotermCode).HasMaxLength(10);
        builder.Property(x => x.IncotermSuffix).HasMaxLength(200);
        builder.HasIndex(x => x.IncotermId);
        builder.Property(x => x.PartialBidsAllowed).HasDefaultValue(true);
        builder.HasIndex(x => x.Status);

        builder.Property(x => x.PrRefs).HasConversion(StringListConverter).Metadata.SetValueComparer(StringListComparer);
        builder.Property(x => x.TechnicalSections).HasConversion(StringListConverter).Metadata.SetValueComparer(StringListComparer);
        builder.Property(x => x.CommercialSections).HasConversion(StringListConverter).Metadata.SetValueComparer(StringListComparer);
        builder.Property(x => x.TechnicalEvaluatorIds).HasConversion(StringListConverter).Metadata.SetValueComparer(StringListComparer);
        builder.Property(x => x.CommercialEvaluatorIds).HasConversion(StringListConverter).Metadata.SetValueComparer(StringListComparer);

        builder.OwnsMany(x => x.Lines, o =>
        {
            o.ToTable("RfqLines");
            o.WithOwner().HasForeignKey("RfqId");
            o.Property<Guid>("Id");
            o.HasKey("Id");
            o.Property(l => l.LineCode).IsRequired().HasMaxLength(50);
            o.Property(l => l.ItemCode).IsRequired().HasMaxLength(50);
            o.Property(l => l.Description).HasMaxLength(500);
            o.Property(l => l.Qty).HasPrecision(18, 4);
            o.Property(l => l.Uom).HasMaxLength(20);
            o.Property(l => l.PrRef).HasMaxLength(50);
            o.Property(l => l.SourcePrLineIds)
                .HasConversion(StringListConverter)
                .Metadata.SetValueComparer(StringListComparer);
            o.HasIndex(l => l.LineCode);
        });

        builder.OwnsMany(x => x.FormItems, o =>
        {
            o.ToTable("RfqFormItems");
            o.WithOwner().HasForeignKey("RfqId");
            o.Property<Guid>("Id");
            o.HasKey("Id");
            o.Property(f => f.Kind).IsRequired().HasMaxLength(20);
            o.Property(f => f.Group).IsRequired().HasMaxLength(20);
            o.Property(f => f.Section).HasMaxLength(200);
            o.Property(f => f.Label).HasMaxLength(300);
            o.Property(f => f.Type).IsRequired().HasMaxLength(20);
            o.Property(f => f.Help).HasMaxLength(500);
        });

        builder.HasMany(x => x.Invitations).WithOne()
            .HasForeignKey(i => i.RfqId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Invitations).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.Events).WithOne()
            .HasForeignKey(e => e.RfqId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Events).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(x => x.DomainEvents);
    }
}
