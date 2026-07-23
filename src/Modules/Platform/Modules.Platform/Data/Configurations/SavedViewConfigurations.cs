using FSH.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Platform.Data.Configurations;

public sealed class FieldRegistryEntryConfiguration : IEntityTypeConfiguration<FieldRegistryEntry>
{
    public void Configure(EntityTypeBuilder<FieldRegistryEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("FieldRegistryEntries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.RecordType, x.FieldKey }).IsUnique();
        builder.Property(x => x.RecordType).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.FieldKey).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Kind).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DataType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class SavedViewConfiguration : IEntityTypeConfiguration<SavedView>
{
    public void Configure(EntityTypeBuilder<SavedView> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("SavedViews");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.RecordType, x.OwnerUserId });
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.RecordType).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.OwnerUserId).HasMaxLength(64);
        builder.Ignore(x => x.DomainEvents);

        builder.HasMany(x => x.Filters)
            .WithOne()
            .HasForeignKey(x => x.SavedViewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Columns)
            .WithOne()
            .HasForeignKey(x => x.SavedViewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Filters).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Columns).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class SavedViewFilterConfiguration : IEntityTypeConfiguration<SavedViewFilter>
{
    public void Configure(EntityTypeBuilder<SavedViewFilter> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("SavedViewFilters");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.SavedViewId, x.Sort });
        builder.Property(x => x.FieldKey).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Operator).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Value).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Value2).HasMaxLength(2000);
    }
}

public sealed class SavedViewColumnConfiguration : IEntityTypeConfiguration<SavedViewColumn>
{
    public void Configure(EntityTypeBuilder<SavedViewColumn> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("SavedViewColumns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.SavedViewId, x.Sort });
        builder.Property(x => x.FieldKey).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Label).HasMaxLength(200);
        builder.Property(x => x.SortDirection).HasConversion<string>().HasMaxLength(10);
    }
}
