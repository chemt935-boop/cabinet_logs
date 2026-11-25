namespace CabinetLogs.Domain.Entities;

/// <summary>
/// Tracks Supabase Storage object paths that failed to delete during log deletion or photo replacement,
/// enabling automatic background retry cleanup.
/// </summary>
public class PendingStorageCleanup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ObjectPath { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastAttemptedAt { get; set; }
    public DateTimeOffset NextRetryAt { get; set; }
}
