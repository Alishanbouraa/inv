// Path: QuickTechSystems.Infrastructure.Data.Configurations/InventoryHistoryConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    public class InventoryHistoryConfiguration : IEntityTypeConfiguration<InventoryHistory>
    {
        public void Configure(EntityTypeBuilder<InventoryHistory> builder)
        {
            builder.HasKey(ih => ih.InventoryHistoryId);

            builder.Property(ih => ih.QuantityChange)
                .HasPrecision(18, 2);

            builder.Property(ih => ih.NewQuantity)
                .HasPrecision(18, 2);

            builder.Property(ih => ih.Type)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(ih => ih.Notes)
                .HasMaxLength(500);

            builder.Property(ih => ih.Timestamp)
                .IsRequired();

            builder.HasOne(ih => ih.Product)
                .WithMany(p => p.InventoryHistories)
                .HasForeignKey(ih => ih.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}