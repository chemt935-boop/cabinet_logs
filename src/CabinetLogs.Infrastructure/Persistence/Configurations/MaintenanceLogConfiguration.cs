using CabinetLogs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CabinetLogs.Infrastructure.Persistence.Configurations;

public sealed class MaintenanceLogConfiguration : IEntityTypeConfiguration<MaintenanceLog>
{
    public void Configure(EntityTypeBuilder<MaintenanceLog> builder)
    {
        builder.ToTable("logs");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(l => l.CabinetId)
            .HasColumnName("cabinet_id")
            .IsRequired();

        builder.Property(l => l.Issue)
            .HasColumnName("issue")
            .HasMaxLength(10000)
            .IsRequired();

        builder.Property(l => l.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.Property(l => l.PhotoPath)
            .HasColumnName("photo_path")
            .HasMaxLength(500);

        builder.Property(l => l.ClientDeviceId)
            .HasColumnName("client_device_id");

        builder.Property(l => l.CreatedByDeviceName)
            .HasColumnName("created_by_device_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(l => l.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasOne(l => l.Cabinet)
            .WithMany(c => c.Logs)
            .HasForeignKey(l => l.CabinetId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_logs_cabinets_cabinet_id");

        builder.HasOne(l => l.ClientDevice)
            .WithMany(d => d.Logs)
            .HasForeignKey(l => l.ClientDeviceId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_logs_client_devices_client_device_id");

        builder.HasIndex(l => new { l.CabinetId, l.OccurredAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_logs_cabinet_id_occurred_at");

        builder.HasIndex(l => l.OccurredAt)
            .IsDescending(true)
            .HasDatabaseName("ix_logs_occurred_at");

        builder.HasIndex(l => l.ClientDeviceId)
            .HasDatabaseName("ix_logs_client_device_id");
    }
}
