using Futurem.Sourcing.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public sealed class InventorySchemaUpgradeService
{
    private readonly AppDbContext _db;
    private readonly ILogger<InventorySchemaUpgradeService> _logger;

    public InventorySchemaUpgradeService(AppDbContext db, ILogger<InventorySchemaUpgradeService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task UpgradeAsync()
    {
        if (!_db.Database.IsRelational()) return;

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `warehouses` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `code` VARCHAR(80) NOT NULL,
              `name` VARCHAR(200) NOT NULL,
              `address` TEXT NULL,
              `contact_name` VARCHAR(120) NULL,
              `contact_phone` VARCHAR(80) NULL,
              `status` VARCHAR(40) NOT NULL DEFAULT 'active',
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              UNIQUE KEY `ux_warehouse_code` (`code`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `warehouse_locations` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `warehouse_id` BIGINT NOT NULL,
              `code` VARCHAR(80) NOT NULL,
              `name` VARCHAR(200) NOT NULL,
              `zone` VARCHAR(80) NULL,
              `aisle` VARCHAR(80) NULL,
              `rack` VARCHAR(80) NULL,
              `bin` VARCHAR(80) NULL,
              `status` VARCHAR(40) NOT NULL DEFAULT 'active',
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              UNIQUE KEY `ux_location_warehouse_code` (`warehouse_id`,`code`),
              KEY `ix_location_warehouse` (`warehouse_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `inventory_lots` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `lot_no` VARCHAR(100) NOT NULL,
              `customer_id` BIGINT NOT NULL,
              `order_product_id` BIGINT NOT NULL,
              `purchase_order_id` BIGINT NOT NULL,
              `purchase_order_line_id` BIGINT NULL,
              `summary_order_id` BIGINT NULL,
              `delivery_notice_id` BIGINT NULL,
              `delivery_notice_line_id` BIGINT NULL,
              `receiving_order_id` BIGINT NOT NULL,
              `receiving_line_id` BIGINT NOT NULL,
              `qc_order_id` BIGINT NOT NULL,
              `qc_order_line_id` BIGINT NOT NULL,
              `supplier_id` BIGINT NOT NULL,
              `warehouse_id` BIGINT NOT NULL,
              `warehouse_location_id` BIGINT NULL,
              `status` VARCHAR(40) NOT NULL DEFAULT 'available',
              `on_hand_quantity` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `locked_quantity` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `on_hand_cartons` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `locked_cartons` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `carton_qty` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `carton_cbm` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `carton_gw_kg` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `carton_nw_kg` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `purchase_unit_price` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `sales_unit_price` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              UNIQUE KEY `ux_inventory_qc_warehouse_location` (`qc_order_line_id`,`warehouse_id`,`warehouse_location_id`),
              KEY `ix_inventory_customer_warehouse_status` (`customer_id`,`warehouse_id`,`status`),
              KEY `ix_inventory_product_warehouse` (`order_product_id`,`warehouse_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `inventory_transactions` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `inventory_lot_id` BIGINT NOT NULL,
              `warehouse_id` BIGINT NOT NULL,
              `warehouse_location_id` BIGINT NULL,
              `transaction_type` VARCHAR(60) NOT NULL,
              `source_type` VARCHAR(60) NOT NULL,
              `source_id` BIGINT NULL,
              `reason` TEXT NOT NULL,
              `quantity_delta` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `cartons_delta` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `quantity_balance` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `cartons_balance` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `locked_quantity_balance` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `locked_cartons_balance` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              KEY `ix_inventory_transaction_lot_time` (`inventory_lot_id`,`created_at`),
              KEY `ix_inventory_transaction_source` (`source_type`,`source_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await AddColumnIfMissingAsync("document_lines", "inventory_lot_id", "ALTER TABLE `document_lines` ADD COLUMN `inventory_lot_id` BIGINT NULL");
        await AddColumnIfMissingAsync("document_lines", "warehouse_id", "ALTER TABLE `document_lines` ADD COLUMN `warehouse_id` BIGINT NULL");
        await AddColumnIfMissingAsync("document_lines", "warehouse_location_id", "ALTER TABLE `document_lines` ADD COLUMN `warehouse_location_id` BIGINT NULL");
        await AddIndexIfMissingAsync("document_lines", "ix_document_line_inventory_lot", "CREATE INDEX `ix_document_line_inventory_lot` ON `document_lines` (`inventory_lot_id`)");

        _logger.LogInformation("Warehouse and traceable inventory schema is ready");
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
