using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    // Create EmployeeSalaryTransactionConfiguration.cs
    public class EmployeeSalaryTransactionConfiguration : IEntityTypeConfiguration<EmployeeSalaryTransaction>
    {
        public void Configure(EntityTypeBuilder<EmployeeSalaryTransaction> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(e => e.TransactionType)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(e => e.TransactionDate)
                .IsRequired();

            builder.Property(e => e.Notes)
                .HasMaxLength(500);

            builder.HasOne(e => e.Employee)
                .WithMany(e => e.SalaryTransactions)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => e.TransactionDate);
            builder.HasIndex(e => e.TransactionType);
        }
    }
}
