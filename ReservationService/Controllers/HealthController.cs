using Microsoft.AspNetCore.Mvc;
using ReservationService.Data;
using Microsoft.EntityFrameworkCore;

namespace ReservationService.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly ReservationServiceContext _context;

    public HealthController(ReservationServiceContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync();
            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
            var appliedMigrations = await _context.Database.GetAppliedMigrationsAsync();

            return Ok(new
            {
                service = "ReservationService",
                status = canConnect ? "UP" : "DOWN",
                database = _context.Database.GetDbConnection().Database,
                migrationsApplied = appliedMigrations.Count(),
                migrationsPending = pendingMigrations.Count()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new
            {
                service = "ReservationService",
                status = "DOWN",
                error = ex.Message
            });
        }
    }
}