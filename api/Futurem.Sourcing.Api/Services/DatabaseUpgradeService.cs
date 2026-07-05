using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public class DatabaseUpgradeService
{
    public const string TargetVersion = "1.0.0";
    private readonly AppDbContext _db;
    private readonly ILogger<DatabaseUpgradeService> _logger;

    public DatabaseUpgradeService(AppDbContext db, ILogger<DatabaseUpgradeService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<object> CheckHealth()
    {
        var canConnect = await _db.Database.CanConnectAsync();
        var pending = await _db.Database.GetPendingMigrationsAsync();
        var applied = await _db.Database.GetAppliedMigrationsAsync();
        var currentVersion = await _db.SchemaVersions.OrderByDescending(x => x.Id).Select(x => x.Version).FirstOrDefaultAsync();
        return new { canConnect, targetVersion = TargetVersion, currentVersion = currentVersion ?? "unknown", pendingMigrations = pending.ToList(), appliedMigrations = applied.ToList() };
    }

    public async Task UpgradeAsync()
    {
        var history = new MigrationHistory { MigrationName = "startup-auto-upgrade", Version = TargetVersion, StartedAt = DateTime.Now, Status = "running", CreatedAt = DateTime.Now };
        try
        {
            _db.MigrationHistories.Add(history);
            await _db.SaveChangesAsync();

            var pending = (await _db.Database.GetPendingMigrationsAsync()).ToList();
            if (pending.Count > 0)
            {
                _logger.LogInformation("Applying {Count} pending migrations", pending.Count);
                await _db.Database.MigrateAsync();
            }
            else
            {
                _logger.LogInformation("No pending EF migrations. Ensuring database is created.");
                await _db.Database.EnsureCreatedAsync();
            }

            var oldVersions = await _db.SchemaVersions.Where(x => x.Status == "current").ToListAsync();
            foreach (var old in oldVersions) old.Status = "archived";
            _db.SchemaVersions.Add(new SchemaVersion { Version = TargetVersion, Status = "current", AppliedAt = DateTime.Now, Notes = "FUTUREM Enterprise V1.0 schema", CreatedAt = DateTime.Now });
            history.Status = "success";
            history.FinishedAt = DateTime.Now;
            history.Message = pending.Count == 0 ? "Database checked. No pending migrations." : $"Applied migrations: {string.Join(',', pending)}";
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            history.Status = "failed";
            history.FinishedAt = DateTime.Now;
            history.Message = ex.Message;
            try { await _db.SaveChangesAsync(); } catch { /* ignore logging failure */ }
            throw;
        }
    }
}
