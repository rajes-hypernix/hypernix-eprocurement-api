using FSH.Framework.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Platform.Data.Configurations;

internal static class PlatformAuditConfiguration
{
    public static void ConfigureAudit<T>(this EntityTypeBuilder<T> builder)
        where T : class, IAuditableEntity
    {
        builder.Property(x => x.CreatedBy).HasMaxLength(64);
        builder.Property(x => x.LastModifiedBy).HasMaxLength(64);
    }

    public static void ConfigureSoftDelete<T>(this EntityTypeBuilder<T> builder)
        where T : class, ISoftDeletable
    {
        builder.Property(x => x.DeletedBy).HasMaxLength(64);
        builder.HasIndex(x => x.IsDeleted);
    }
}
