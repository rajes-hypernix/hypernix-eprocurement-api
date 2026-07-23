using FSH.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Platform.Data.Configurations;

public sealed class OrgUnitConfiguration : IEntityTypeConfiguration<OrgUnit>
{
    public void Configure(EntityTypeBuilder<OrgUnit> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("OrgUnits");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.Type, x.Code }).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Type).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.ConfigureAudit();
        builder.Ignore(x => x.DomainEvents);
        builder.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FormTemplateConfiguration : IEntityTypeConfiguration<FormTemplate>
{
    public void Configure(EntityTypeBuilder<FormTemplate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("FormTemplates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Key).IsUnique();
        builder.Property(x => x.Key).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.ConfigureAudit();
        builder.Ignore(x => x.DomainEvents);

        builder.HasMany(x => x.Questions)
            .WithOne()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Questions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class FormTemplateQuestionConfiguration : IEntityTypeConfiguration<FormTemplateQuestion>
{
    public void Configure(EntityTypeBuilder<FormTemplateQuestion> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("FormTemplateQuestions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.TemplateId, x.Order }).IsUnique();
        builder.Property(x => x.Label).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Type).IsRequired().HasMaxLength(40);
        builder.Property(x => x.ConfigJson).HasMaxLength(4000);
        builder.Property(x => x.Help).HasMaxLength(500);
        builder.ConfigureAudit();
        builder.Ignore(x => x.DomainEvents);
    }
}
