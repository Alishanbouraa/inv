// Path: QuickTechSystems.Infrastructure/Data/Configurations/BusinessSettingConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    public class BusinessSettingConfiguration : IEntityTypeConfiguration<BusinessSetting>
    {
        public void Configure(EntityTypeBuilder<BusinessSetting> builder)
        {
            builder.HasKey(s => s.Id);

            builder.Property(s => s.Key)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(s => s.Value)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(s => s.Description)
                .HasMaxLength(500);

            builder.Property(s => s.Group)
                .HasMaxLength(50);

            builder.Property(s => s.DataType)
                .HasMaxLength(20);

            builder.Property(s => s.ModifiedBy)
                .HasMaxLength(100);

            builder.HasIndex(s => s.Key)
                .IsUnique();

            builder.HasIndex(s => s.Group);
        }
    }
}