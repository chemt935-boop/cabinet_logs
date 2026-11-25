namespace CabinetLogs.Domain.Entities;

public class MaintenanceLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CabinetId { get; set; }
    public string Issue { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string? PhotoPath { get; set; }
    public Guid? ClientDeviceId { get; set; }

    /// <summary>
    /// Snapshot of the submitting PC/mobile device name at the time this log was created.
    /// </summary>
    public string CreatedByDeviceName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Cabinet Cabinet { get; set; } = null!;
    public ClientDevice? ClientDevice { get; set; }
    public ICollection<LogPhoto> Photos { get; set; } = new List<LogPhoto>();
}
