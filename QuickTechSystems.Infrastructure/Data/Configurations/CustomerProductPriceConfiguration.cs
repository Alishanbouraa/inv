// Infrastructure/Data/Configurations/CustomerProductPriceConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    public class CustomerProductPriceConfiguration : IEntityTypeConfiguration<CustomerProductPrice>
    {
        public void Configure(EntityTypeBuilder<CustomerProductPrice> builder)
        {
            builder.HasKey(cpp => cpp.CustomerProductPriceId);

            builder.Property(cpp => cpp.Price)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.HasOne(cpp => cpp.Customer)
                .WithMany()
                .HasForeignKey(cpp => cpp.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(cpp => cpp.Product)
                .WithMany()
                .HasForeignKey(cpp => cpp.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(cpp => new { cpp.CustomerId, cpp.ProductId }).IsUnique();
        }
    }
}