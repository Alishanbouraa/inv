using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

public class DrawerTransactionConfiguration : IEntityTypeConfiguration<DrawerTransaction>
{
    public void Configure(EntityTypeBuilder<DrawerTransaction> builder)
    {
        builder.HasKey(t => t.TransactionId);

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

        builder.Property(t => t.Timestamp)
            .IsRequired();

        builder.HasOne(t => t.Drawer)
            .WithMany(d => d.Transactions)
            .HasForeignKey(t => t.DrawerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.DrawerId);
        builder.HasIndex(t => t.Timestamp);
    }
}

