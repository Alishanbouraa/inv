// Path: QuickTechSystems.Infrastructure/Data/Configurations/RestaurantTableConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    public class RestaurantTableConfiguration : IEntityTypeConfiguration<RestaurantTable>
    {
        public void Configure(EntityTypeBuilder<RestaurantTable> builder)
        {
            builder.HasKey(t => t.Id);

            builder.Property(t => t.TableNumber)
                .IsRequired();

            builder.Property(t => t.Status)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(t => t.Description)
                .HasMaxLength(500);

            // Create a unique index on TableNumber
            builder.HasIndex(t => t.TableNumber)
                .IsUnique();
        }
    }
}