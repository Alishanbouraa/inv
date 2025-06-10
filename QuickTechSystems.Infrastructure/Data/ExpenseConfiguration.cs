using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
    {
        public void Configure(EntityTypeBuilder<Expense> builder)
        {
            builder.HasKey(e => e.ExpenseId);

            builder.Property(e => e.Reason)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(e => e.Date)
                .IsRequired();

            builder.Property(e => e.Notes)
                .HasMaxLength(500);

            builder.Property(e => e.Category)
                .HasMaxLength(50);

            builder.Property(e => e.CreatedAt)
                .IsRequired();

            builder.HasIndex(e => e.Date);
            builder.HasIndex(e => e.Category);
        }
    }
}
