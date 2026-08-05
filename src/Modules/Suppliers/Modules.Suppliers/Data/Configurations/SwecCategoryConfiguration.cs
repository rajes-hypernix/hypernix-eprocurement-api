using FSH.Modules.Suppliers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Suppliers.Data.Configurations;

public sealed class SwecCategoryConfiguration : IEntityTypeConfiguration<SwecCategory>
{
    public void Configure(EntityTypeBuilder<SwecCategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("SwecCategories");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasMaxLength(20);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
        builder.Property(x => x.ParentCode).HasMaxLength(20);
        builder.Property(x => x.PathText).IsRequired().HasMaxLength(400);
        builder.HasIndex(x => x.ParentCode);
        builder.ConfigureAudit();
    }
}
