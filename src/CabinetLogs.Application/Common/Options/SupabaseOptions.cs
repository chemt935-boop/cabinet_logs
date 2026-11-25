namespace CabinetLogs.Application.Common.Options;

public sealed class SupabaseOptions
{
    public const string SectionName = "Supabase";

    /// <summary>
    /// Supabase project URL, e.g. https://your-project-ref.supabase.co
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Optional Supabase anon/public key (only if required by server integrations).
    /// </summary>
    public string AnonKey { get; set; } = string.Empty;

    /// <summary>
    /// Privileged Supabase service_role key. Server-side only; NEVER expose to Flutter clients.
    /// </summary>
    public string ServiceRoleKey { get; set; } = string.Empty;

    /// <summary>
    /// Private Supabase Storage bucket name for log photos.
    /// </summary>
    public string StorageBucket { get; set; } = "cabinet-log-photos";

    /// <summary>
    /// Expiry duration in seconds for generated signed photo URLs.
    /// </summary>
    public int SignedUrlExpirySeconds { get; set; } = 300;
}
