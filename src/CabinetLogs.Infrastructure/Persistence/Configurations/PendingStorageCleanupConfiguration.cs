using CabinetLogs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CabinetLogs.Infrastructure.Persistence.Configurations;

public sealed class PendingStorageCleanupConfiguration : IEntityTypeConfiguration<PendingStorageCleanup>
{
    public void Configure(EntityTypeBuilder<PendingStorageCleanup> builder)
    {
        builder.ToTable("pending_storage_cleanups");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(p => p.ObjectPath)
            .HasColumnName("object_path")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.AttemptCount)
            .HasColumnName("attempt_count")
            .IsRequired();

        builder.Property(p => p.LastError)
            .HasColumnName("last_error")
            .HasMaxLength(1000);

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.LastAttemptedAt)
            .HasColumnName("last_attempted_at");

        builder.Property(p => p.NextRetryAt)
            .HasColumnName("next_retry_at")
            .IsRequired();

        builder.HasIndex(p => p.NextRetryAt)
            .HasDatabaseName("ix_pending_storage_cleanups_next_retry_at");
    }
}
