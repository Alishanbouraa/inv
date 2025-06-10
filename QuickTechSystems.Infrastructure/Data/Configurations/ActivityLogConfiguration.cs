using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Infrastructure.Data.Configurations
{
    public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
    {
        public void Configure(EntityTypeBuilder<ActivityLog> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(e => e.Action)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.Details)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(e => e.Timestamp)
                .IsRequired();

            builder.Property(e => e.IpAddress)
                .HasMaxLength(50);

            builder.Property(e => e.UserAgent)
                .HasMaxLength(250);

            builder.Property(e => e.SessionId)
                .HasMaxLength(100);

            builder.Property(e => e.ErrorMessage)
                .HasMaxLength(500);

            builder.Property(e => e.ModuleName)
                .HasMaxLength(100);

            builder.Property(e => e.EntityId)
                .HasMaxLength(100);

            builder.Property(e => e.EntityType)
                .HasMaxLength(100);

            builder.Property(e => e.OldValue)
                .HasMaxLength(4000);

            builder.Property(e => e.NewValue)
                .HasMaxLength(4000);

            // Indexes for common queries
            builder.HasIndex(e => e.UserId);
            builder.HasIndex(e => e.Timestamp);
            builder.HasIndex(e => e.Action);
            builder.HasIndex(e => e.ModuleName);
            builder.HasIndex(e => e.EntityType);
            builder.HasIndex(e => new { e.EntityType, e.EntityId });
            builder.HasIndex(e => e.IsSuccess);

            // Default value for IsSuccess
            builder.Property(e => e.IsSuccess)
                .HasDefaultValue(true);

            // Default value for Timestamp
            builder.Property(e => e.Timestamp)
                .HasDefaultValueSql("GETUTCDATE()");
        }
    }
}