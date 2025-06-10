// Path: QuickTechSystems.Infrastructure.Data.Configurations/SupplierInvoiceConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    public class SupplierInvoiceConfiguration : IEntityTypeConfiguration<SupplierInvoice>
    {
        public void Configure(EntityTypeBuilder<SupplierInvoice> builder)
        {
            builder.HasKey(si => si.SupplierInvoiceId);

            builder.Property(si => si.InvoiceNumber)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(si => si.TotalAmount)
                .HasPrecision(18, 2);

            builder.Property(si => si.CalculatedAmount)
                .HasPrecision(18, 2);

            builder.Property(si => si.Status)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(si => si.Notes)
                .HasMaxLength(500);

            builder.HasOne(si => si.Supplier)
                .WithMany()
                .HasForeignKey(si => si.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(si => new { si.SupplierId, si.InvoiceNumber })
                .IsUnique();
        }
    }
}