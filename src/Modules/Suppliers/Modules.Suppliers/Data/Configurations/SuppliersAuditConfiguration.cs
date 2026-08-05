using FSH.Framework.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Suppliers.Data.Configurations;

internal static class SuppliersAuditConfiguration
{
    public static void ConfigureAudit<T>(this EntityTypeBuilder<T> builder)
        where T : class, IAuditableEntity
    {
        builder.Property(x => x.CreatedBy).HasMaxLength(64);
        builder.Property(x => x.LastModifiedBy).HasMaxLength(64);
    }
}
