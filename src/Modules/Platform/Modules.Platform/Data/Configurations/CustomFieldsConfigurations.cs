using FSH.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Platform.Data.Configurations;

public sealed class CustomFieldDefConfiguration : IEntityTypeConfiguration<CustomFieldDef>
{
    public void Configure(EntityTypeBuilder<CustomFieldDef> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("CustomFieldDefs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DataType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.RefEntity).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ListKey).HasMaxLength(50);
        builder.Property(x => x.Scope).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.DisplayType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.HelpText).HasMaxLength(500);
        builder.ConfigureAudit();
        builder.Ignore(x => x.DomainEvents);

        builder.OwnsMany(x => x.Applications, applications =>
        {
            applications.ToTable("CustomFieldDefApplications");
            applications.WithOwner().HasForeignKey("CustomFieldDefId");
            applications.HasKey(x => x.Id);
            applications.Property(x => x.Id).ValueGeneratedNever();
            applications.Property(x => x.RecordType).IsRequired().HasConversion<string>().HasMaxLength(30);
            applications.HasIndex("CustomFieldDefId", nameof(CustomFieldDefApplication.RecordType)).IsUnique();
        });
        builder.Navigation(x => x.Applications).HasField("_applications").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class CustomFieldValueConfiguration : IEntityTypeConfiguration<CustomFieldValue>
{
    public void Configure(EntityTypeBuilder<CustomFieldValue> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("CustomFieldValues");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.RecordType).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.DataType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ValueText).HasMaxLength(2000);
        builder.Property(x => x.ValueNumber).HasPrecision(18, 4);
        builder.Property(x => x.ValueListCode).HasMaxLength(50);
        builder.Property(x => x.ValueLabel).HasMaxLength(200);
        builder.ConfigureAudit();
        builder.Ignore(x => x.DomainEvents);

        // Two partial unique indexes rather than one nullable-aware constraint (Postgres treats
        // NULL LineId as distinct from NULL, so a single index wouldn't stop duplicate header
        // values) — one row per field per record header, one row per field per record line.
        builder.HasIndex(x => new { x.CustomFieldDefId, x.RecordType, x.RecordId })
            .IsUnique()
            .HasFilter("\"LineId\" IS NULL");
        builder.HasIndex(x => new { x.CustomFieldDefId, x.RecordType, x.RecordId, x.LineId })
            .IsUnique()
            .HasFilter("\"LineId\" IS NOT NULL");
        builder.HasIndex(x => new { x.RecordType, x.RecordId });
    }
}
