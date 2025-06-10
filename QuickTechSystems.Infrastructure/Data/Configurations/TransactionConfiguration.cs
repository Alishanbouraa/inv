using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
    {
        public void Configure(EntityTypeBuilder<Transaction> builder)
        {
            builder.HasKey(t => t.TransactionId);

            builder.Property(t => t.TransactionId)
                .ValueGeneratedOnAdd();

            builder.Property(t => t.TotalAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(t => t.PaidAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            // Removed Balance property

            builder.Property(t => t.TransactionDate)
                .IsRequired();

            builder.Property(t => t.TransactionType)
                .IsRequired()
                .HasConversion<string>();

            builder.Property(t => t.Status)
                .IsRequired()
                .HasConversion<string>();

            builder.HasIndex(t => t.TransactionDate);
            builder.HasIndex(t => t.Status);
            builder.HasIndex(t => t.TransactionType);

            // Navigation property configurations
            builder.HasOne(t => t.Customer)
                .WithMany(c => c.Transactions)
                .HasForeignKey(t => t.CustomerId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasMany(t => t.TransactionDetails)
                .WithOne(td => td.Transaction)
                .HasForeignKey(td => td.TransactionId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            // Configure cascade delete behavior
            builder.Metadata.FindNavigation(nameof(Transaction.TransactionDetails))
                ?.SetPropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}