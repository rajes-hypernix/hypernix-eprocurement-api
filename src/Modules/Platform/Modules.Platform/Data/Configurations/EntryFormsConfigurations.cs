using FSH.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Platform.Data.Configurations;

public sealed class EntryFormDefConfiguration : IEntityTypeConfiguration<EntryFormDef>
{
    public void Configure(EntityTypeBuilder<EntryFormDef> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("EntryFormDefs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.RecordType).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.ConfigureAudit();
        builder.Ignore(x => x.DomainEvents);

        builder.HasMany(x => x.Groups).WithOne()
            .HasForeignKey(x => x.EntryFormDefId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Groups).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.Fields).WithOne()
            .HasForeignKey(x => x.EntryFormDefId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Fields).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class EntryFormGroupConfiguration : IEntityTypeConfiguration<EntryFormGroup>
{
    public void Configure(EntityTypeBuilder<EntryFormGroup> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("EntryFormGroups");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.EntryFormDefId);
    }
}

public sealed class EntryFormFieldConfiguration : IEntityTypeConfiguration<EntryFormField>
{
    public void Configure(EntityTypeBuilder<EntryFormField> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("EntryFormFields");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FieldKey).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.EntryFormDefId);
        builder.HasIndex(x => x.GroupId);
    }
}

public sealed class EntryFormRoleMapConfiguration : IEntityTypeConfiguration<EntryFormRoleMap>
{
    public void Configure(EntityTypeBuilder<EntryFormRoleMap> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("EntryFormRoleMaps");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.RecordType).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Role).IsRequired().HasMaxLength(100);
        builder.Ignore(x => x.DomainEvents);
        builder.HasIndex(x => new { x.RecordType, x.Role }).IsUnique();
    }
}
