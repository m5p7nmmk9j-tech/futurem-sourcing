using System.Diagnostics;
using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/monitor-center")]
public class MonitorCenterController : ControllerBase
{
    private readonly AppDbContext _db;
    public MonitorCenterController(AppDbContext db) { _db = db; }

    [HttpGet("overview")]
    public async Task<ActionResult<object>> Overview()
    {
        var process = Process.GetCurrentProcess();
        var root = Path.GetPathRoot(AppContext.BaseDirectory) ?? "/";
        var drive = new DriveInfo(root);
        var mysqlOk = await _db.Database.CanConnectAsync();
        var cache = new CacheService(HttpContext.RequestServices.GetRequiredService<IConfiguration>(), HttpContext.RequestServices.GetRequiredService<ILogger<CacheService>>());
        var redisOk = await cache.IsAvailableAsync();
        return new
        {
            api = new { status = "running", version = "1.0.0-rc", uptimeSeconds = (DateTime.Now - process.StartTime).TotalSeconds },
            server = new { machine = Environment.MachineName, os = Environment.OSVersion.ToString(), processorCount = Environment.ProcessorCount },
            process = new { memoryMb = Math.Round(process.WorkingSet64 / 1024m / 1024m, 2), threads = process.Threads.Count, handles = process.HandleCount },
            disk = new { name = drive.Name, totalGb = Math.Round(drive.TotalSize / 1024m / 1024m / 1024m, 2), freeGb = Math.Round(drive.AvailableFreeSpace / 1024m / 1024m / 1024m, 2), usedPercent = Math.Round((1 - drive.AvailableFreeSpace / (decimal)drive.TotalSize) * 100, 2) },
            mysql = new { status = mysqlOk ? "ok" : "down" },
            redis = new { status = redisOk ? "ok" : "fallback" },
            database = new
            {
                users = await _db.UserAccounts.CountAsync(),
                products = await _db.Products.CountAsync(),
                customers = await _db.Customers.CountAsync(),
                suppliers = await _db.Suppliers.CountAsync(),
                auditLogs = await _db.AuditLogs.CountAsync(),
                backups = await _db.BackupHistories.CountAsync()
            }
        };
    }

    [HttpGet("health")]
    public async Task<ActionResult<object>> Health()
    {
        var mysqlOk = await _db.Database.CanConnectAsync();
        var cache = new CacheService(HttpContext.RequestServices.GetRequiredService<IConfiguration>(), HttpContext.RequestServices.GetRequiredService<ILogger<CacheService>>());
        var redisOk = await cache.IsAvailableAsync();
        var healthy = mysqlOk;
        return StatusCode(healthy ? 200 : 503, new
        {
            status = healthy ? "healthy" : "unhealthy",
            api = "ok",
            mysql = mysqlOk ? "ok" : "down",
            redis = redisOk ? "ok" : "fallback",
            time = DateTime.Now
        });
    }

    [HttpGet("logs")]
    public async Task<ActionResult<object>> Logs()
    {
        return new
        {
            loginLogs = await _db.LoginLogs.OrderByDescending(x => x.Id).Take(20).ToListAsync(),
            auditLogs = await _db.AuditLogs.OrderByDescending(x => x.Id).Take(20).ToListAsync(),
            migrations = await _db.MigrationHistories.OrderByDescending(x => x.Id).Take(20).ToListAsync(),
            backups = await _db.BackupHistories.OrderByDescending(x => x.Id).Take(20).ToListAsync()
        };
    }
}
