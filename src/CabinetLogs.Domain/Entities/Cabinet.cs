namespace CabinetLogs.Domain.Entities;

public class Cabinet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<MaintenanceLog> Logs { get; set; } = new List<MaintenanceLog>();
}
