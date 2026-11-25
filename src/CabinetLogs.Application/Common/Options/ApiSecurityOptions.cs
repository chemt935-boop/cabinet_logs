namespace CabinetLogs.Application.Common.Options;

public sealed class ApiSecurityOptions
{
    public const string SectionName = "ApiSecurity";

    /// <summary>
    /// When false (default in Version 1), no API key is required (shared internal access).
    /// Enable this before exposing the API to a public network.
    /// </summary>
    public bool RequireApiKey { get; set; } = false;

    /// <summary>
    /// HTTP header name carrying the shared API key when RequireApiKey is true.
    /// </summary>
    public string ApiKeyHeaderName { get; set; } = "X-Api-Key";

    /// <summary>
    /// Shared API key value expected in the header when RequireApiKey is true.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
