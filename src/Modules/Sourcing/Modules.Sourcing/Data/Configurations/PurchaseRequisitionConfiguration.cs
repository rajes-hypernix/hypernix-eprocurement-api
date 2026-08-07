using FSH.Modules.Sourcing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Sourcing.Data.Configurations;

public sealed class PurchaseRequisitionConfiguration : IEntityTypeConfiguration<PurchaseRequisition>
{
    public void Configure(EntityTypeBuilder<PurchaseRequisition> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PurchaseRequisitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Requestor).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Department).HasMaxLength(200);
        builder.Property(x => x.DepartmentCode).HasMaxLength(50);
        builder.Property(x => x.Location).HasMaxLength(200);
        builder.Property(x => x.LocationCode).HasMaxLength(50);
        builder.Property(x => x.Category).HasMaxLength(200);
        builder.Property(x => x.CategoryCode).HasMaxLength(50);
        builder.Property(x => x.Job).HasMaxLength(200);
        builder.Property(x => x.JobCode).HasMaxLength(50);
        builder.Property(x => x.Memo).HasMaxLength(2000);
        builder.Property(x => x.CostCentre).HasMaxLength(50);
        builder.Property(x => x.Project).HasMaxLength(200);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.HeaderStatus).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ShipToAdhoc).HasMaxLength(400);
        builder.HasIndex(x => x.HeaderStatus);
        builder.Ignore(x => x.HasShipTo);

        builder.HasMany(x => x.Lines).WithOne()
            .HasForeignKey(l => l.PurchaseRequisitionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(x => x.DomainEvents);
    }
}
