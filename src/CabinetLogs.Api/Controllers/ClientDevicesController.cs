using CabinetLogs.Application.Common;
using CabinetLogs.Application.DTOs.ClientDevices;
using CabinetLogs.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CabinetLogs.Api.Controllers;

[ApiController]
[Route("api/v1/client-devices")]
[Tags("Client Devices")]
[Produces("application/json")]
public sealed class ClientDevicesController : ControllerBase
{
    private readonly IClientDeviceService _service;

    public ClientDevicesController(IClientDeviceService service)
    {
        _service = service;
    }

    /// <summary>
    /// Register a new PC, laptop, or mobile client device installation.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ClientDeviceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientDeviceDto>> Create(
        [FromBody] CreateClientDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// List registered client devices with optional search or installationKey filter.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ClientDeviceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ClientDeviceDto>>> GetAll(
        [FromQuery] ClientDeviceQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get a registered client device by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClientDeviceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientDeviceDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Update a registered client device's name or platform. Historical logs keep their original device name snapshot.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ClientDeviceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientDeviceDto>> Update(
        Guid id,
        [FromBody] UpdateClientDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Unregister/delete a client device. Associated logs are preserved with clientDeviceId set to null.
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
}
