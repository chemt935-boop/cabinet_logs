using CabinetLogs.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CabinetLogs.Api.Controllers;

[ApiController]
[Route("api/v1/health")]
[Tags("Health")]
[Produces("application/json")]
public sealed class HealthController : ControllerBase
{
    private readonly IApplicationDbContext _db;

    public HealthController(IApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Check API liveness and PostgreSQL database connectivity.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Check(CancellationToken cancellationToken)
    {
        var dbHealthy = false;
        try
        {
            dbHealthy = await _db.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            dbHealthy = false;
        }

        var payload = new
        {
            status = dbHealthy ? "Healthy" : "Unhealthy",
            database = dbHealthy ? "Connected" : "Unreachable",
            utcTime = DateTimeOffset.UtcNow
        };

        return dbHealthy
            ? Ok(payload)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, payload);
    }
}
