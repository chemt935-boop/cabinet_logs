using CabinetLogs.Domain.Enums;

namespace CabinetLogs.Domain.Entities;

public class ClientDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DevicePlatform? Platform { get; set; }
    public string? InstallationKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<MaintenanceLog> Logs { get; set; } = new List<MaintenanceLog>();
}
