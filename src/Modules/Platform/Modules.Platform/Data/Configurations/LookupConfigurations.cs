using FSH.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Platform.Data.Configurations;

public sealed class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Countries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(2);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class StateConfiguration : IEntityTypeConfiguration<State>
{
    public void Configure(EntityTypeBuilder<State> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("States");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.CountryId, x.Code }).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(10);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Ignore(x => x.DomainEvents);
        builder.HasOne<Country>().WithMany().HasForeignKey(x => x.CountryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Cities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.StateId, x.Name }).IsUnique();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Ignore(x => x.DomainEvents);
        builder.HasOne<State>().WithMany().HasForeignKey(x => x.StateId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Banks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.CountryCode, x.Name }).IsUnique();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.SwiftCode).HasMaxLength(20);
        builder.Property(x => x.CountryCode).IsRequired().HasMaxLength(2);
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class CustomListConfiguration : IEntityTypeConfiguration<CustomList>
{
    public void Configure(EntityTypeBuilder<CustomList> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("CustomLists");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Key).IsUnique();
        builder.Property(x => x.Key).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Ignore(x => x.DomainEvents);

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.ListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class CustomListItemConfiguration : IEntityTypeConfiguration<CustomListItem>
{
    public void Configure(EntityTypeBuilder<CustomListItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("CustomListItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.ListId, x.Code }).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(200);
        builder.Ignore(x => x.DomainEvents);
    }
}
