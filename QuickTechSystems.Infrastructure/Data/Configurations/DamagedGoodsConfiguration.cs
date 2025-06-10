// Path: QuickTechSystems.Infrastructure.Data.Configurations/DamagedGoodsConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    public class DamagedGoodsConfiguration : IEntityTypeConfiguration<DamagedGoods>
    {
        public void Configure(EntityTypeBuilder<DamagedGoods> builder)
        {
            builder.HasKey(d => d.DamagedGoodsId);

            builder.Property(d => d.ProductName)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(d => d.Barcode)
                .HasMaxLength(50);

            builder.Property(d => d.Reason)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(d => d.LossAmount)
                .HasPrecision(18, 2);

            builder.Property(d => d.CategoryName)
                .HasMaxLength(100);

            builder.HasOne(d => d.Product)
                .WithMany()
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}