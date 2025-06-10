// Path: QuickTechSystems.Infrastructure.Data.Configurations/SupplierInvoiceDetailConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    public class SupplierInvoiceDetailConfiguration : IEntityTypeConfiguration<SupplierInvoiceDetail>
    {
        public void Configure(EntityTypeBuilder<SupplierInvoiceDetail> builder)
        {
            builder.HasKey(sid => sid.SupplierInvoiceDetailId);

            builder.Property(sid => sid.Quantity)
                .HasPrecision(18, 2);

            builder.Property(sid => sid.PurchasePrice)
                .HasPrecision(18, 2);

            builder.Property(sid => sid.TotalPrice)
                .HasPrecision(18, 2);

            builder.Property(sid => sid.BoxBarcode)
                .HasMaxLength(50);

            builder.Property(sid => sid.BoxPurchasePrice)
                .HasPrecision(18, 2);

            builder.Property(sid => sid.BoxSalePrice)
                .HasPrecision(18, 2);

            builder.HasOne(sid => sid.SupplierInvoice)
                .WithMany(si => si.Details)
                .HasForeignKey(sid => sid.SupplierInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(sid => sid.Product)
                .WithMany()
                .HasForeignKey(sid => sid.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}