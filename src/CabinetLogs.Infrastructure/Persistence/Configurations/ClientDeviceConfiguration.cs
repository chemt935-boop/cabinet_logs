using CabinetLogs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CabinetLogs.Infrastructure.Persistence.Configurations;

public sealed class ClientDeviceConfiguration : IEntityTypeConfiguration<ClientDevice>
{
    public void Configure(EntityTypeBuilder<ClientDevice> builder)
    {
        builder.ToTable("client_devices");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(d => d.Name)
            .HasColumnName("name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(d => d.Platform)
            .HasColumnName("platform")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(d => d.InstallationKey)
            .HasColumnName("installation_key")
            .HasMaxLength(200);

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(d => d.InstallationKey)
            .IsUnique()
            .HasFilter("installation_key IS NOT NULL")
            .HasDatabaseName("ix_client_devices_installation_key");
    }
}
