using System.Globalization;
using System.Text.Json;
using CabinetLogs.Application.Common;
using CabinetLogs.Application.Common.Exceptions;
using CabinetLogs.Application.DTOs.Logs;
using CabinetLogs.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CabinetLogs.Api.Controllers;

[ApiController]
[Route("api/v1/logs")]
[Tags("Logs")]
[Produces("application/json")]
public sealed class LogsController : ControllerBase
{
    private readonly ILogService _service;
    private readonly JsonSerializerOptions _jsonOptions;

    public LogsController(
        ILogService service,
        IOptions<JsonOptions> jsonOptions)
    {
        _service = service;
        _jsonOptions = jsonOptions.Value.JsonSerializerOptions;
    }

    /// <summary>
    /// Create a maintenance log and automatically create/reuse its cabinet category.
    /// Accepts either application/json (no photos) or multipart/form-data (with optional photos under 'photos' or 'photo').
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(LogDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<LogDto>> Create(CancellationToken cancellationToken)
    {
        CreateLogRequest request;
        var photoFiles = new List<PhotoUploadFile>();
        var ownedStreams = new List<Stream>();

        try
        {
            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync(cancellationToken);
                request = ParseCreateForm(form);
                await ExtractPhotosAsync(form.Files, photoFiles, ownedStreams, cancellationToken);
            }
            else if (IsJsonContentType(Request.ContentType))
            {
                request = await DeserializeJsonBodyAsync<CreateLogRequest>(cancellationToken);
            }
            else
            {
                throw new UnsupportedMediaTypeException(
                    "Content-Type must be 'application/json' or 'multipart/form-data'.");
            }

            var created = await _service.CreateAsync(request, photoFiles, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        finally
        {
            foreach (var stream in ownedStreams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// List, search, filter, sort, and paginate maintenance logs.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<LogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<LogDto>>> GetAll(
        [FromQuery] LogQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get a single maintenance log by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LogDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Update a maintenance log's cabinet, issue, date/time, or photos.
    /// Accepts either application/json or multipart/form-data (to add new photos and/or remove specific photo IDs).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<LogDto>> Update(
        Guid id,
        CancellationToken cancellationToken)
    {
        UpdateLogRequest request;
        var photoFiles = new List<PhotoUploadFile>();
        var ownedStreams = new List<Stream>();

        try
        {
            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync(cancellationToken);
                request = ParseUpdateForm(form);
                await ExtractPhotosAsync(form.Files, photoFiles, ownedStreams, cancellationToken);
            }
            else if (IsJsonContentType(Request.ContentType))
            {
                request = await DeserializeJsonBodyAsync<UpdateLogRequest>(cancellationToken);
            }
            else
            {
                throw new UnsupportedMediaTypeException(
                    "Content-Type must be 'application/json' or 'multipart/form-data'.");
            }

            var updated = await _service.UpdateAsync(id, request, photoFiles, cancellationToken);
            return Ok(updated);
        }
        finally
        {
            foreach (var stream in ownedStreams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Delete a maintenance log and attempt to clean up all its photos from Supabase Storage.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Generate a short-lived signed URL for the first attached photo of a log (backwards-compatible).
    /// </summary>
    [HttpGet("{id:guid}/photo-url")]
    [ProducesResponseType(typeof(PhotoUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PhotoUrlResponse>> GetPhotoUrl(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetPhotoUrlAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Generate short-lived signed URLs for all photos attached to a log.
    /// </summary>
    [HttpGet("{id:guid}/photos")]
    [ProducesResponseType(typeof(IReadOnlyList<LogPhotoUrlDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LogPhotoUrlDto>>> GetPhotoUrls(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetPhotoUrlsAsync(id, cancellationToken);
        return Ok(result);
    }

    private async Task<T> DeserializeJsonBodyAsync<T>(CancellationToken cancellationToken)
    {
        var payload = await Request.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken);
        if (payload is null)
        {
            throw new BadRequestException("Request body cannot be empty.");
        }

        return payload;
    }

    private static bool IsJsonContentType(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType)
        && contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);

    private static CreateLogRequest ParseCreateForm(IFormCollection form)
    {
        return new CreateLogRequest
        {
            CabinetName = form["cabinetName"].ToString(),
            Issue = form["issue"].ToString(),
            OccurredAt = ParseOptionalDateTimeOffset(form["occurredAt"].ToString(), "occurredAt"),
            ClientDeviceId = ParseOptionalGuid(form["clientDeviceId"].ToString(), "clientDeviceId"),
            CreatedByDeviceName = string.IsNullOrWhiteSpace(form["createdByDeviceName"].ToString())
                ? null
                : form["createdByDeviceName"].ToString()
        };
    }

    private static UpdateLogRequest ParseUpdateForm(IFormCollection form)
    {
        return new UpdateLogRequest
        {
            CabinetName = form["cabinetName"].ToString(),
            Issue = form["issue"].ToString(),
            OccurredAt = ParseOptionalDateTimeOffset(form["occurredAt"].ToString(), "occurredAt"),
            RemovePhoto = ParseOptionalBool(form["removePhoto"].ToString(), "removePhoto"),
            RemovePhotoIds = ParseOptionalGuidList(form["removePhotoIds"], "removePhotoIds")
        };
    }

    private static DateTimeOffset? ParseOptionalDateTimeOffset(string raw, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(
                raw.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            throw new BadRequestException($"Invalid ISO 8601 date/time value for '{fieldName}'.", new Dictionary<string, string[]>
            {
                [fieldName] = [$"'{raw}' is not a valid ISO 8601 date/time value."]
            });
        }

        return parsed;
    }

    private static Guid? ParseOptionalGuid(string raw, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!Guid.TryParse(raw.Trim(), out var parsed))
        {
            throw new BadRequestException($"Invalid UUID value for '{fieldName}'.", new Dictionary<string, string[]>
            {
                [fieldName] = [$"'{raw}' is not a valid UUID."]
            });
        }

        return parsed;
    }

    private static List<Guid>? ParseOptionalGuidList(Microsoft.Extensions.Primitives.StringValues values, string fieldName)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var result = new List<Guid>();
        foreach (var rawEntry in values)
        {
            if (string.IsNullOrWhiteSpace(rawEntry))
            {
                continue;
            }

            foreach (var part in rawEntry.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Guid.TryParse(part, out var parsed))
                {
                    throw new BadRequestException($"Invalid UUID value '{part}' in '{fieldName}'.", new Dictionary<string, string[]>
                    {
                        [fieldName] = [$"'{part}' is not a valid UUID."]
                    });
                }
                result.Add(parsed);
            }
        }

        return result.Count > 0 ? result : null;
    }

    private static bool ParseOptionalBool(string raw, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (!bool.TryParse(raw.Trim(), out var parsed))
        {
            throw new BadRequestException($"Invalid boolean value for '{fieldName}'.", new Dictionary<string, string[]>
            {
                [fieldName] = [$"'{raw}' is not a valid boolean ('true' or 'false')."]
            });
        }

        return parsed;
    }

    private static async Task ExtractPhotosAsync(
        IFormFileCollection files,
        List<PhotoUploadFile> target,
        List<Stream> ownedStreams,
        CancellationToken cancellationToken)
    {
        // Support both repeated "photos" fields AND legacy single "photo" field.
        foreach (var file in files)
        {
            if (!string.Equals(file.Name, "photos", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(file.Name, "photo", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rawStream = file.OpenReadStream();
            if (rawStream.CanSeek)
            {
                ownedStreams.Add(rawStream);
                target.Add(new PhotoUploadFile(rawStream, file.Length, file.ContentType, file.FileName));
            }
            else
            {
                var buffer = new MemoryStream();
                await using (rawStream)
                {
                    await rawStream.CopyToAsync(buffer, cancellationToken);
                }
                buffer.Position = 0;
                ownedStreams.Add(buffer);
                target.Add(new PhotoUploadFile(buffer, buffer.Length, file.ContentType, file.FileName));
            }
        }
    }
}
