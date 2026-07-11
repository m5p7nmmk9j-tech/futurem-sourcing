using Futurem.Sourcing.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public sealed class SummaryReservationSchemaUpgradeService
{
    private readonly AppDbContext _db;
    private readonly ILogger<SummaryReservationSchemaUpgradeService> _logger;

    public SummaryReservationSchemaUpgradeService(
        AppDbContext db,
        ILogger<SummaryReservationSchemaUpgradeService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task UpgradeAsync()
    {
        if (!_db.Database.IsRelational()) return;

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `summary_order_items` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `summary_order_id` BIGINT NOT NULL,
              `purchase_order_id` BIGINT NOT NULL,
              `purchase_order_line_id` BIGINT NOT NULL,
              `order_product_id` BIGINT NOT NULL,
              `supplier_id` BIGINT NOT NULL,
              `reserved_cartons` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `reserved_quantity` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `reservation_status` VARCHAR(40) NOT NULL DEFAULT 'draft_reserved',
              `confirmed_at` DATETIME NULL,
              `released_at` DATETIME NULL,
              `release_reason` TEXT NULL,
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              KEY `ix_summary_item_po_line_status` (`purchase_order_line_id`,`reservation_status`),
              KEY `ix_summary_item_summary_status` (`summary_order_id`,`reservation_status`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await AddColumnIfMissingAsync("summary_orders", "container_type", "ALTER TABLE `summary_orders` ADD COLUMN `container_type` VARCHAR(40) NULL");
        await AddColumnIfMissingAsync("summary_orders", "warehouse_id", "ALTER TABLE `summary_orders` ADD COLUMN `warehouse_id` BIGINT NULL");
        await AddColumnIfMissingAsync("summary_orders", "planned_delivery_date", "ALTER TABLE `summary_orders` ADD COLUMN `planned_delivery_date` DATETIME NULL");
        await AddColumnIfMissingAsync("summary_orders", "confirmed_at", "ALTER TABLE `summary_orders` ADD COLUMN `confirmed_at` DATETIME NULL");
        await AddColumnIfMissingAsync("summary_orders", "total_quantity", "ALTER TABLE `summary_orders` ADD COLUMN `total_quantity` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("summary_orders", "total_cartons", "ALTER TABLE `summary_orders` ADD COLUMN `total_cartons` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("summary_orders", "total_cbm", "ALTER TABLE `summary_orders` ADD COLUMN `total_cbm` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("summary_orders", "total_gross_weight_kg", "ALTER TABLE `summary_orders` ADD COLUMN `total_gross_weight_kg` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("summary_orders", "total_net_weight_kg", "ALTER TABLE `summary_orders` ADD COLUMN `total_net_weight_kg` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("summary_orders", "purchase_amount", "ALTER TABLE `summary_orders` ADD COLUMN `purchase_amount` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("summary_orders", "sales_amount", "ALTER TABLE `summary_orders` ADD COLUMN `sales_amount` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("summary_orders", "expected_profit", "ALTER TABLE `summary_orders` ADD COLUMN `expected_profit` DECIMAL(18,2) NOT NULL DEFAULT 0");

        await _db.Database.ExecuteSqlRawAsync("UPDATE `summary_orders` SET `currency` = 'RMB'");
        _logger.LogInformation("Summary reservation schema is ready");
    }

    private async Task AddColumnIfMissingAsync(string table, string column, string alterSql)
    {
        var count = await _db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS `Value` FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = {0} AND column_name = {1}",
            table,
            column).SingleAsync();
        if (count == 0) await _db.Database.ExecuteSqlRawAsync(alterSql);
    }
}
