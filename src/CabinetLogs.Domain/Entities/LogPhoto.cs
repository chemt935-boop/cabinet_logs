namespace CabinetLogs.Domain.Entities;

public class LogPhoto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LogId { get; set; }
    public string PhotoPath { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public MaintenanceLog Log { get; set; } = null!;
}
