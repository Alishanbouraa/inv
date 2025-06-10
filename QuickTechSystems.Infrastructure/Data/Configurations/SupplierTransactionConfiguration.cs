using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    public class SupplierTransactionConfiguration : IEntityTypeConfiguration<SupplierTransaction>
    {
        public void Configure(EntityTypeBuilder<SupplierTransaction> builder)
        {
            builder.HasKey(st => st.SupplierTransactionId);

            builder.Property(st => st.Amount)
                .HasPrecision(18, 2);

            builder.Property(st => st.TransactionType)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(st => st.Reference)
                .HasMaxLength(100);

            builder.Property(st => st.Notes)
                .HasMaxLength(500);

            builder.HasOne(st => st.Supplier)
                .WithMany(s => s.Transactions)
                .HasForeignKey(st => st.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}