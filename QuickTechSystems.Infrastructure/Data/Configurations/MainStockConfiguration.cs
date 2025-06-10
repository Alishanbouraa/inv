// Path: QuickTechSystems.Infrastructure.Data.Configurations/MainStockConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    public class MainStockConfiguration : IEntityTypeConfiguration<MainStock>
    {
        public void Configure(EntityTypeBuilder<MainStock> builder)
        {
            builder.HasKey(m => m.MainStockId);

            builder.Property(m => m.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(m => m.Barcode)
                .HasMaxLength(50);

            builder.Property(m => m.BoxBarcode)
                .HasMaxLength(50);

            builder.Property(m => m.Description)
                .HasMaxLength(500);

            builder.Property(m => m.PurchasePrice)
                .HasPrecision(18, 2);

            builder.Property(m => m.SalePrice)
                .HasPrecision(18, 2);

            builder.Property(m => m.CurrentStock)
                .HasPrecision(18, 2);

            builder.Property(m => m.BoxPurchasePrice)
                .HasPrecision(18, 2);

            builder.Property(m => m.BoxSalePrice)
                .HasPrecision(18, 2);

            builder.HasIndex(m => m.Barcode)
                .IsUnique();

            builder.HasIndex(m => m.BoxBarcode);

            builder.Property(m => m.Speed)
                .HasMaxLength(50)
                .IsRequired(false);

            builder.Property(m => m.ImagePath)
                .HasMaxLength(500)
                .IsRequired(false);

            builder.HasOne(m => m.Category)
                .WithMany()
                .HasForeignKey(m => m.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(m => m.Supplier)
                .WithMany()
                .HasForeignKey(m => m.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.Property(m => m.WholesalePrice)
            .HasPrecision(18, 2);

            builder.Property(m => m.BoxWholesalePrice)
                .HasPrecision(18, 2);
        }
    }
}