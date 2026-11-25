using System.Text.Json;
using CabinetLogs.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CabinetLogs.Api.Middleware;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;

        if (exception is AppException appEx)
        {
            if (appEx.StatusCode >= 500)
            {
                _logger.LogError(appEx, "Application service error ({StatusCode}) [TraceId: {TraceId}]", appEx.StatusCode, traceId);
            }
            else
            {
                _logger.LogWarning("Request failed with {StatusCode}: {Message} [TraceId: {TraceId}]", appEx.StatusCode, appEx.Message, traceId);
            }

            httpContext.Response.StatusCode = appEx.StatusCode;

            if (appEx.Errors is { Count: > 0 })
            {
                var validationProblem = new ValidationProblemDetails(appEx.Errors)
                {
                    Status = appEx.StatusCode,
                    Title = appEx.Title,
                    Detail = appEx.Message,
                    Instance = httpContext.Request.Path
                };
                validationProblem.Extensions["traceId"] = traceId;

                await httpContext.Response.WriteAsJsonAsync(validationProblem, cancellationToken);
                return true;
            }

            var problem = new ProblemDetails
            {
                Status = appEx.StatusCode,
                Title = appEx.Title,
                Detail = appEx.Message,
                Instance = httpContext.Request.Path
            };
            problem.Extensions["traceId"] = traceId;

            await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
            return true;
        }

        if (exception is BadHttpRequestException badHttpEx)
        {
            var statusCode = badHttpEx.StatusCode is >= 400 and < 500
                ? badHttpEx.StatusCode
                : StatusCodes.Status400BadRequest;

            var title = statusCode == StatusCodes.Status413PayloadTooLarge
                ? "Payload Too Large"
                : "Bad Request";

            var detail = statusCode == StatusCodes.Status413PayloadTooLarge
                ? "The request payload exceeds the maximum allowed size."
                : "The HTTP request was malformed or could not be read.";

            _logger.LogWarning(badHttpEx, "Bad HTTP request ({StatusCode}) [TraceId: {TraceId}]", statusCode, traceId);

            httpContext.Response.StatusCode = statusCode;
            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            };
            problem.Extensions["traceId"] = traceId;

            await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
            return true;
        }

        if (exception is JsonException)
        {
            _logger.LogWarning(exception, "Invalid JSON payload [TraceId: {TraceId}]", traceId);

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = "The JSON request body is invalid or contains an unsupported value format.",
                Instance = httpContext.Request.Path
            };
            problem.Extensions["traceId"] = traceId;

            await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
            return true;
        }

        // Never expose stack traces, connection strings, or internal errors to clients.
        _logger.LogError(exception, "Unhandled server exception [TraceId: {TraceId}]", traceId);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var serverProblem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Detail = "An unexpected error occurred while processing the request.",
            Instance = httpContext.Request.Path
        };
        serverProblem.Extensions["traceId"] = traceId;

        await httpContext.Response.WriteAsJsonAsync(serverProblem, cancellationToken);
        return true;
    }
}
