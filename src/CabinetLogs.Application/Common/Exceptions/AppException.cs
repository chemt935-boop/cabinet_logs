namespace CabinetLogs.Application.Common.Exceptions;

public abstract class AppException : Exception
{
    public int StatusCode { get; }
    public string Title { get; }
    public IDictionary<string, string[]>? Errors { get; }

    protected AppException(
        int statusCode,
        string title,
        string message,
        IDictionary<string, string[]>? errors = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Title = title;
        Errors = errors;
    }
}

public sealed class BadRequestException : AppException
{
    public BadRequestException(string message, IDictionary<string, string[]>? errors = null)
        : base(400, "Bad Request", message, errors)
    {
    }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string message)
        : base(404, "Not Found", message)
    {
    }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string message)
        : base(409, "Conflict", message)
    {
    }
}

public sealed class PayloadTooLargeException : AppException
{
    public PayloadTooLargeException(string message)
        : base(413, "Payload Too Large", message)
    {
    }
}

public sealed class UnsupportedMediaTypeException : AppException
{
    public UnsupportedMediaTypeException(string message)
        : base(415, "Unsupported Media Type", message)
    {
    }
}

public sealed class ExternalServiceException : AppException
{
    public ExternalServiceException(string message, Exception? innerException = null)
        : base(502, "External Service Error", message, innerException: innerException)
    {
    }
}
