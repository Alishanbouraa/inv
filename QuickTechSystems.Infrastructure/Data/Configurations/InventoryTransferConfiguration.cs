// Path: QuickTechSystems.Infrastructure.Data.Configurations/InventoryTransferConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    public class InventoryTransferConfiguration : IEntityTypeConfiguration<InventoryTransfer>
    {
        public void Configure(EntityTypeBuilder<InventoryTransfer> builder)
        {
            builder.HasKey(it => it.InventoryTransferId);

            builder.Property(it => it.Quantity)
                .HasPrecision(18, 2);

            builder.Property(it => it.Notes)
                .HasMaxLength(500);

            builder.Property(it => it.ReferenceNumber)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(it => it.TransferredBy)
                .HasMaxLength(100)
                .IsRequired();

            builder.HasOne(it => it.MainStock)
                .WithMany()
                .HasForeignKey(it => it.MainStockId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(it => it.Product)
                .WithMany()
                .HasForeignKey(it => it.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}