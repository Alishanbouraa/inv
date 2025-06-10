// Path: QuickTechSystems.Infrastructure.Data.Configurations/LowStockHistoryConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    public class LowStockHistoryConfiguration : IEntityTypeConfiguration<LowStockHistory>
    {
        public void Configure(EntityTypeBuilder<LowStockHistory> builder)
        {
            builder.HasKey(h => h.LowStockHistoryId);

            builder.Property(h => h.ProductName)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(h => h.AlertDate)
                .IsRequired();

            builder.Property(h => h.CashierId)
                .HasMaxLength(50);

            builder.Property(h => h.CashierName)
                .HasMaxLength(100);

            builder.HasOne(h => h.Product)
                .WithMany()
                .HasForeignKey(h => h.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(h => h.AlertDate);
            builder.HasIndex(h => h.ProductId);
        }
    }
}