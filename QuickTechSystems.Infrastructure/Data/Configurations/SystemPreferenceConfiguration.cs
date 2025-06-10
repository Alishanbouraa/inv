// Path: QuickTechSystems.Infrastructure/Data/Configurations/SystemPreferenceConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    public class SystemPreferenceConfiguration : IEntityTypeConfiguration<SystemPreference>
    {
        public void Configure(EntityTypeBuilder<SystemPreference> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.UserId)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(p => p.PreferenceKey)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(p => p.PreferenceValue)
                .IsRequired()
                .HasMaxLength(500);

            builder.HasIndex(p => new { p.UserId, p.PreferenceKey })
                .IsUnique();
        }
    }
}