using Futurem.Sourcing.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public sealed class ContainerReservationSchemaUpgradeService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ContainerReservationSchemaUpgradeService> _logger;

    public ContainerReservationSchemaUpgradeService(
        AppDbContext db,
        ILogger<ContainerReservationSchemaUpgradeService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task UpgradeAsync()
    {
        if (!_db.Database.IsRelational()) return;

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `inventory_reservations` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `container_load_id` BIGINT NOT NULL,
              `inventory_lot_id` BIGINT NOT NULL,
              `customer_id` BIGINT NOT NULL,
              `warehouse_id` BIGINT NOT NULL,
              `reserved_quantity` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `reserved_cartons` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `status` VARCHAR(40) NOT NULL DEFAULT 'active',
              `locked_at` DATETIME NOT NULL,
              `expires_at` DATETIME NOT NULL,
              `released_at` DATETIME NULL,
              `release_reason` TEXT NULL,
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              KEY `ix_reservation_container_status` (`container_load_id`,`status`),
              KEY `ix_reservation_lot_status_expiry` (`inventory_lot_id`,`status`,`expires_at`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await AddColumnIfMissingAsync(
            "container_loads",
            "customer_id",
            "ALTER TABLE `container_loads` ADD COLUMN `customer_id` BIGINT NULL");
        await AddColumnIfMissingAsync(
            "container_loads",
            "warehouse_id",
            "ALTER TABLE `container_loads` ADD COLUMN `warehouse_id` BIGINT NULL");
        await AddColumnIfMissingAsync(
            "container_loads",
            "inventory_locked_at",
            "ALTER TABLE `container_loads` ADD COLUMN `inventory_locked_at` DATETIME NULL");
        await AddColumnIfMissingAsync(
            "container_loads",
            "inventory_lock_expires_at",
            "ALTER TABLE `container_loads` ADD COLUMN `inventory_lock_expires_at` DATETIME NULL");
        await AddIndexIfMissingAsync(
            "container_loads",
            "ix_container_customer_warehouse_status",
            "CREATE INDEX `ix_container_customer_warehouse_status` ON `container_loads` (`customer_id`,`warehouse_id`,`status`)");

        _logger.LogInformation("Container inventory reservation schema is ready");
    }

    private async Task AddColumnIfMissingAsync(string table, string column, string alterSql)
    {
        var count = await _db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS `Value` FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = {0} AND column_name = {1}",
            table,
            column).SingleAsync();
        if (count == 0) await _db.Database.ExecuteSqlRawAsync(alterSql);
    }

    private async Task AddIndexIfMissingAsync(string table, string index, string createSql)
    {
        var count = await _db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS `Value` FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = {0} AND index_name = {1}",
            table,
            index).SingleAsync();
        if (count == 0) await _db.Database.ExecuteSqlRawAsync(createSql);
    }
}
