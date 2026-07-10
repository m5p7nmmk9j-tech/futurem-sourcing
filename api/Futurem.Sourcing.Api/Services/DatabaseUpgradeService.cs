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
        List<string> pending = [];
        MigrationHistory? history = null;
        try
        {
            pending = (await _db.Database.GetPendingMigrationsAsync()).ToList();
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
            await EnsureV1ProductColumnsAsync();

            history = new MigrationHistory { MigrationName = "startup-auto-upgrade", Version = TargetVersion, StartedAt = DateTime.Now, Status = "running", CreatedAt = DateTime.Now };
            _db.MigrationHistories.Add(history);
            await _db.SaveChangesAsync();

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
            if (history is not null)
            {
                history.Status = "failed";
                history.FinishedAt = DateTime.Now;
                history.Message = ex.Message;
                try { await _db.SaveChangesAsync(); } catch { /* ignore logging failure */ }
            }
            throw;
        }
    }

    private async Task EnsureV1ProductColumnsAsync()
    {
        await AddColumnIfMissingAsync("products", "purchase_price", "ALTER TABLE `products` ADD COLUMN `purchase_price` DECIMAL(18,4) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("products", "carton_qty", "ALTER TABLE `products` ADD COLUMN `carton_qty` DECIMAL(18,4) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("products", "carton_length_cm", "ALTER TABLE `products` ADD COLUMN `carton_length_cm` DECIMAL(18,4) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("products", "carton_width_cm", "ALTER TABLE `products` ADD COLUMN `carton_width_cm` DECIMAL(18,4) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("products", "carton_height_cm", "ALTER TABLE `products` ADD COLUMN `carton_height_cm` DECIMAL(18,4) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("products", "carton_gw_kg", "ALTER TABLE `products` ADD COLUMN `carton_gw_kg` DECIMAL(18,4) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("products", "carton_nw_kg", "ALTER TABLE `products` ADD COLUMN `carton_nw_kg` DECIMAL(18,4) NOT NULL DEFAULT 0");
    }

    private async Task AddColumnIfMissingAsync(string table, string column, string alterSql)
    {
        var exists = await _db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS `Value` FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = {0} AND column_name = {1}", table, column)
            .SingleAsync();
        if (exists > 0) return;
        await _db.Database.ExecuteSqlRawAsync(alterSql);
    }
}
