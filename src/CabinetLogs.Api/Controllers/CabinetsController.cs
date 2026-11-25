using CabinetLogs.Application.Common;
using CabinetLogs.Application.DTOs.Cabinets;
using CabinetLogs.Application.DTOs.Logs;
using CabinetLogs.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CabinetLogs.Api.Controllers;

[ApiController]
[Route("api/v1/cabinets")]
[Tags("Cabinets")]
[Produces("application/json")]
public sealed class CabinetsController : ControllerBase
{
    private readonly ICabinetService _service;

    public CabinetsController(ICabinetService service)
    {
        _service = service;
    }

    /// <summary>
    /// List and search cabinets (auto-created categories) along with their log counts.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CabinetSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<CabinetSummaryDto>>> GetAll(
        [FromQuery] CabinetQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get details and log count for a single cabinet by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CabinetDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CabinetDetailDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get paginated, filterable logs belonging to a specific cabinet.
    /// </summary>
    [HttpGet("{id:guid}/logs")]
    [ProducesResponseType(typeof(PagedResult<LogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<LogDto>>> GetLogs(
        Guid id,
        [FromQuery] LogQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetLogsByCabinetIdAsync(id, query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Delete a cabinet. When force=false (default), refuses deletion with 409 Conflict if the cabinet still has logs.
    /// When force=true, deletes the cabinet, all its logs, and attempts photo cleanup.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        await _service.DeleteAsync(id, force, cancellationToken);
        return NoContent();
    }
}
