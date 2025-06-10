// Path: QuickTechSystems.Infrastructure.Data.Configurations/ProductConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.HasKey(p => p.ProductId);
            builder.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(200);
            builder.Property(p => p.Barcode)
                .HasMaxLength(50);
            builder.Property(p => p.BoxBarcode)
                .HasMaxLength(50);
            builder.Property(p => p.Description)
                .HasMaxLength(500);
            builder.Property(p => p.PurchasePrice)
                .HasPrecision(18, 2);
            builder.Property(p => p.SalePrice)
                .HasPrecision(18, 2);
            builder.Property(p => p.BoxPurchasePrice)
                .HasPrecision(18, 2);
            builder.Property(p => p.BoxSalePrice)
                .HasPrecision(18, 2);
            builder.Property(p => p.CurrentStock);

            builder.HasIndex(p => p.Barcode)
                .IsUnique();
            builder.HasIndex(p => p.BoxBarcode);
            builder.Property(p => p.Speed)
                .HasMaxLength(50)
                .IsRequired(false);

            builder.Property(p => p.ImagePath)
                .HasMaxLength(500)
                .IsRequired(false);

            builder.HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Add the relationship to MainStock
            builder.HasOne(p => p.MainStock)
                .WithMany(m => m.Products)
                .HasForeignKey(p => p.MainStockId)
                .OnDelete(DeleteBehavior.SetNull);
            // Path: QuickTechSystems.Infrastructure.Data.Configurations/ProductConfiguration.cs

            // Add inside the Configure method
            builder.Property(p => p.WholesalePrice)
                .HasPrecision(18, 2);

            builder.Property(p => p.BoxWholesalePrice)
                .HasPrecision(18, 2);
        }
    }
}