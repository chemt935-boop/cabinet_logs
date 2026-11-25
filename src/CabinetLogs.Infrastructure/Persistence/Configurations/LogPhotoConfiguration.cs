using CabinetLogs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CabinetLogs.Infrastructure.Persistence.Configurations;

public sealed class LogPhotoConfiguration : IEntityTypeConfiguration<LogPhoto>
{
    public void Configure(EntityTypeBuilder<LogPhoto> builder)
    {
        builder.ToTable("log_photos");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(p => p.LogId)
            .HasColumnName("log_id")
            .IsRequired();

        builder.Property(p => p.PhotoPath)
            .HasColumnName("photo_path")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasOne(p => p.Log)
            .WithMany(l => l.Photos)
            .HasForeignKey(p => p.LogId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_log_photos_logs_log_id");

        builder.HasIndex(p => new { p.LogId, p.SortOrder })
            .HasDatabaseName("ix_log_photos_log_id_sort_order");
    }
}
