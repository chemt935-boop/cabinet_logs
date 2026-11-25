using System.Security.Cryptography;
using System.Text;
using CabinetLogs.Application.Common.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CabinetLogs.Api.Middleware;

/// <summary>
/// Optional shared API key protection for deployments where the no-login v1 API is exposed beyond a trusted network.
/// Disabled by default (ApiSecurity:RequireApiKey = false).
/// </summary>
public sealed class OptionalApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<ApiSecurityOptions> _optionsMonitor;

    public OptionalApiKeyMiddleware(
        RequestDelegate next,
        IOptionsMonitor<ApiSecurityOptions> optionsMonitor)
    {
        _next = next;
        _optionsMonitor = optionsMonitor;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var options = _optionsMonitor.CurrentValue;
        if (!options.RequireApiKey)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var headerName = string.IsNullOrWhiteSpace(options.ApiKeyHeaderName)
            ? "X-Api-Key"
            : options.ApiKeyHeaderName;

        if (!context.Request.Headers.TryGetValue(headerName, out var providedValues)
            || string.IsNullOrEmpty(options.ApiKey)
            || !FixedTimeEquals(providedValues.ToString(), options.ApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = $"A valid '{headerName}' header is required.",
                Instance = context.Request.Path
            };
            problem.Extensions["traceId"] = context.TraceIdentifier;
            await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
            return;
        }

        await _next(context);
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
