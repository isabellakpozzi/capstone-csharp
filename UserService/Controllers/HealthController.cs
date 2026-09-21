using Microsoft.AspNetCore.Mvc;
using UserService.Data;
using Microsoft.EntityFrameworkCore;

namespace UserService.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly UserServiceContext _context;

    public HealthController(UserServiceContext context)
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
                service = "UserService",
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
                service = "UserService",
                status = "DOWN",
                error = ex.Message
            });
        }
    }
}