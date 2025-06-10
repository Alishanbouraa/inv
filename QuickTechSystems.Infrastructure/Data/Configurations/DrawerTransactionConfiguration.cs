using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    public class DrawerTransactionConfiguration : IEntityTypeConfiguration<DrawerTransaction>
    {
        public void Configure(EntityTypeBuilder<DrawerTransaction> builder)
        {
            builder.HasKey(t => t.TransactionId);

            builder.Property(t => t.DrawerId)
                .IsRequired();

            builder.Property(t => t.Timestamp)
                .IsRequired();

            builder.Property(t => t.Type)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(t => t.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(t => t.Balance)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(t => t.Notes)
                .HasMaxLength(500);

            builder.Property(t => t.ActionType)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(t => t.Description)
                .HasMaxLength(500);

            builder.Property(t => t.TransactionReference)
                .HasMaxLength(100);

            builder.Property(t => t.IsVoided)
                .HasDefaultValue(false);

            builder.Property(t => t.PaymentMethod)
                .HasMaxLength(50);

            // Relationships
            builder.HasOne(t => t.Drawer)
                .WithMany(d => d.Transactions)
                .HasForeignKey(t => t.DrawerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            builder.HasIndex(t => t.DrawerId);
            builder.HasIndex(t => t.Timestamp);
            builder.HasIndex(t => t.TransactionReference);
            builder.HasIndex(t => t.ActionType);
            builder.HasIndex(t => t.Type);
            builder.HasIndex(t => t.IsVoided);

            // Seed Data (if needed)
            // builder.HasData(
            //     new DrawerTransaction { ... }
            // );
        }
    }
}