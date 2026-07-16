using System.Text.Json;
using FSH.Modules.Suppliers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FSH.Modules.Suppliers.Data.Configurations;

public sealed class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    private static readonly ValueConverter<List<string>, string> CategoriesConverter = new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

    private static readonly ValueComparer<List<string>> CategoriesComparer = new(
        (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
        v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode(StringComparison.Ordinal))),
        v => v.ToList());

    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Vendors");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.RegisteredName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.RegistrationNo).IsRequired().HasMaxLength(60);
        builder.Property(x => x.TaxId).IsRequired().HasMaxLength(60);
        builder.Property(x => x.Type).IsRequired().HasMaxLength(20);
        builder.Property(x => x.LlrcTier).HasMaxLength(20);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Region).IsRequired().HasMaxLength(50);
        builder.Property(x => x.State).IsRequired().HasMaxLength(100);
        builder.Property(x => x.City).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Country).IsRequired().HasMaxLength(2);
        builder.Property(x => x.Rating).HasPrecision(18, 2);
        builder.Property(x => x.PaymentTerms).IsRequired().HasMaxLength(50);
        builder.Property(x => x.CreditLimit).HasPrecision(18, 2);

        builder.Property(x => x.Categories)
            .HasConversion(CategoriesConverter)
            .Metadata.SetValueComparer(CategoriesComparer);

        builder.OwnsMany(x => x.Contacts, o =>
        {
            o.ToTable("VendorContacts");
            o.WithOwner().HasForeignKey("VendorId");
            o.Property<Guid>("Id");
            o.HasKey("Id");
            o.Property(c => c.Name).IsRequired().HasMaxLength(200);
            o.Property(c => c.Role).HasMaxLength(100);
            o.Property(c => c.Email).HasMaxLength(200);
            o.Property(c => c.Phone).HasMaxLength(50);
        });

        builder.OwnsMany(x => x.Addresses, o =>
        {
            o.ToTable("VendorAddresses");
            o.WithOwner().HasForeignKey("VendorId");
            o.Property<Guid>("Id");
            o.HasKey("Id");
            o.Property(a => a.Type).IsRequired().HasMaxLength(50);
            o.Property(a => a.Line).HasMaxLength(300);
            o.Property(a => a.City).HasMaxLength(100);
            o.Property(a => a.State).HasMaxLength(100);
            o.Property(a => a.Country).IsRequired().HasMaxLength(2);
            o.Property(a => a.Postcode).HasMaxLength(20);
        });

        builder.OwnsMany(x => x.BankAccounts, o =>
        {
            o.ToTable("VendorBankAccounts");
            o.WithOwner().HasForeignKey("VendorId");
            o.Property<Guid>("Id");
            o.HasKey("Id");
            o.Property(b => b.Bank).IsRequired().HasMaxLength(200);
            o.Property(b => b.AccountNo).HasMaxLength(50);
            o.Property(b => b.Swift).HasMaxLength(20);
            o.Property(b => b.Currency).IsRequired().HasMaxLength(3);
        });

        builder.OwnsMany(x => x.Certifications, o =>
        {
            o.ToTable("VendorCertifications");
            o.WithOwner().HasForeignKey("VendorId");
            o.Property<Guid>("Id");
            o.HasKey("Id");
            o.Property(c => c.Name).IsRequired().HasMaxLength(200);
            o.Property(c => c.Number).HasMaxLength(100);
            o.Property(c => c.ValidTo).HasMaxLength(50);
            o.Property(c => c.Status).IsRequired().HasMaxLength(20);
        });

        builder.OwnsMany(x => x.Currencies, o =>
        {
            o.ToTable("VendorCurrencies");
            o.WithOwner().HasForeignKey("VendorId");
            o.Property<Guid>("Id");
            o.HasKey("Id");
            o.Property(c => c.Code).IsRequired().HasMaxLength(3);
        });

        builder.Ignore(x => x.DomainEvents);
    }
}
