using FSH.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Platform.Data.Configurations;

public sealed class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Settings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Key).IsUnique();
        builder.Property(x => x.Key).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Value).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ValueKind).IsRequired().HasMaxLength(40);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Description).HasMaxLength(400);
        builder.ConfigureAudit();
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Currencies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Symbol).IsRequired().HasMaxLength(6);
        builder.ConfigureAudit();
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ExchangeRates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(3);
        builder.Property(x => x.RateToBase).HasPrecision(18, 8);
        builder.Property(x => x.EnteredByUserId).IsRequired().HasMaxLength(80);
        builder.HasIndex(x => new { x.CurrencyCode, x.EffectiveDate, x.CreatedOnUtc });
        builder.ConfigureAudit();
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class TaxCodeConfiguration : IEntityTypeConfiguration<TaxCode>
{
    public void Configure(EntityTypeBuilder<TaxCode> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("TaxCodes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(10);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(60);
        builder.Property(x => x.RatePct).HasPrecision(7, 4);
        builder.ConfigureAudit();
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class IncotermConfiguration : IEntityTypeConfiguration<Incoterm>
{
    public void Configure(EntityTypeBuilder<Incoterm> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Incoterms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(80);
        builder.ConfigureAudit();
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.ItemCode).IsUnique();
        builder.Property(x => x.ItemCode).IsRequired().HasMaxLength(40);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Uom).IsRequired().HasMaxLength(20);
        builder.ConfigureAudit();
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class PaymentTermConfiguration : IEntityTypeConfiguration<PaymentTerm>
{
    public void Configure(EntityTypeBuilder<PaymentTerm> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PaymentTerms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.DiscountPct).HasPrecision(7, 4);
        builder.ConfigureAudit();
        builder.Ignore(x => x.DomainEvents);
        builder.OwnsMany(x => x.Rows, rows =>
        {
            rows.ToTable("PaymentScheduleRows");
            rows.WithOwner().HasForeignKey("PaymentTermId");
            rows.HasKey(x => x.Id);
            rows.Property(x => x.Id).ValueGeneratedNever();
            rows.Property(x => x.Basis).HasConversion<string>().HasMaxLength(20);
            rows.Property(x => x.Percent).HasPrecision(7, 4);
            rows.Property(x => x.Label).HasMaxLength(80);
        });
        builder.Navigation(x => x.Rows).HasField("_rows").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Locations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(40);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(80);
        builder.ConfigureAudit();
        builder.Ignore(x => x.DomainEvents);
        builder.OwnsMany(x => x.Addresses, addr =>
        {
            addr.ToTable("LocationAddresses");
            addr.WithOwner().HasForeignKey("LocationId");
            addr.HasKey(x => x.Id);
            addr.Property(x => x.Id).ValueGeneratedNever();
            addr.Property(x => x.Label).IsRequired().HasMaxLength(60);
            addr.Property(x => x.Line1).IsRequired().HasMaxLength(120);
            addr.Property(x => x.Line2).HasMaxLength(120);
            addr.Property(x => x.City).IsRequired().HasMaxLength(60);
            addr.Property(x => x.State).IsRequired().HasMaxLength(60);
            addr.Property(x => x.Postcode).IsRequired().HasMaxLength(20);
            addr.Property(x => x.Country).IsRequired().HasMaxLength(2);
        });
        builder.Navigation(x => x.Addresses).HasField("_addresses").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class NumberingSchemeConfiguration : IEntityTypeConfiguration<NumberingScheme>
{
    public void Configure(EntityTypeBuilder<NumberingScheme> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("NumberingSchemes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.RecordType).IsUnique();
        builder.Property(x => x.RecordType).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Prefix).IsRequired().HasMaxLength(12);
        builder.ConfigureAudit();
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("NumberSequences");
        builder.HasKey(x => new { x.Prefix, x.Year });
        builder.Property(x => x.Prefix).IsRequired().HasMaxLength(12);
        builder.ConfigureAudit();
    }
}
