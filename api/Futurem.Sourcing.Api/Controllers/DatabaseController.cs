using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/database")]
public class DatabaseController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly DatabaseUpgradeService _upgrade;
    public DatabaseController(AppDbContext db, DatabaseUpgradeService upgrade) { _db = db; _upgrade = upgrade; }

    [HttpGet("health")]
    public async Task<ActionResult<object>> Health() => Ok(await _upgrade.CheckHealth());

    [HttpPost("upgrade")]
    public async Task<IActionResult> Upgrade()
    {
        await _upgrade.UpgradeAsync();
        return Ok(new { ok = true, version = DatabaseUpgradeService.TargetVersion });
    }

    [HttpGet("versions")]
    public async Task<ActionResult<object>> Versions()
    {
        var versions = await _db.SchemaVersions.OrderByDescending(x => x.Id).Take(100).ToListAsync();
        var history = await _db.MigrationHistories.OrderByDescending(x => x.Id).Take(100).ToListAsync();
        return new { versions, history };
    }
}
