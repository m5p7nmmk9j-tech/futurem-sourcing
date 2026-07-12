using Futurem.Sourcing.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public sealed class ContainerConfirmationSchemaUpgradeService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ContainerConfirmationSchemaUpgradeService> _logger;

    public ContainerConfirmationSchemaUpgradeService(
        AppDbContext db,
        ILogger<ContainerConfirmationSchemaUpgradeService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task UpgradeAsync()
    {
        if (!_db.Database.IsRelational()) return;

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `container_load_sources` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `container_load_id` BIGINT NOT NULL,
              `inventory_reservation_id` BIGINT NOT NULL,
              `inventory_lot_id` BIGINT NOT NULL,
              `customer_id` BIGINT NOT NULL,
              `warehouse_id` BIGINT NOT NULL,
              `order_product_id` BIGINT NOT NULL,
              `purchase_order_id` BIGINT NOT NULL,
              `purchase_order_line_id` BIGINT NULL,
              `summary_order_id` BIGINT NULL,
              `receiving_order_id` BIGINT NOT NULL,
              `qc_order_id` BIGINT NOT NULL,
              `supplier_id` BIGINT NOT NULL,
              `planned_quantity` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `planned_cartons` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `actual_quantity` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `actual_cartons` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `purchase_unit_price` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `sales_unit_price` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `actual_cbm` DECIMAL(18,6) NOT NULL DEFAULT 0,
              `actual_gross_weight_kg` DECIMAL(18,4) NOT NULL DEFAULT 0,
              `status` VARCHAR(40) NOT NULL DEFAULT 'loaded',
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              UNIQUE KEY `ux_container_source_reservation` (`container_load_id`,`inventory_reservation_id`),
              KEY `ix_container_source_summary` (`container_load_id`,`summary_order_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `finance_record_lines` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `finance_record_id` BIGINT NOT NULL,
              `source_key` VARCHAR(240) NOT NULL,
              `line_type` VARCHAR(60) NOT NULL,
              `source_type` VARCHAR(80) NOT NULL,
              `source_id` BIGINT NULL,
              `order_product_id` BIGINT NULL,
              `quantity` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `unit_price` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `amount` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `paid_amount` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `description` TEXT NULL,
              `status` VARCHAR(40) NOT NULL DEFAULT 'pending',
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              UNIQUE KEY `ux_finance_record_line_source` (`source_key`),
              KEY `ix_finance_record_line_record_time` (`finance_record_id`,`created_at`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await AddColumnIfMissingAsync("shipments", "customer_id", "ALTER TABLE `shipments` ADD COLUMN `customer_id` BIGINT NULL");
        await AddColumnIfMissingAsync("shipments", "warehouse_id", "ALTER TABLE `shipments` ADD COLUMN `warehouse_id` BIGINT NULL");
        await AddColumnIfMissingAsync("shipments", "container_type", "ALTER TABLE `shipments` ADD COLUMN `container_type` VARCHAR(40) NULL");
        await AddColumnIfMissingAsync("shipments", "container_no", "ALTER TABLE `shipments` ADD COLUMN `container_no` VARCHAR(120) NULL");
        await AddColumnIfMissingAsync("shipments", "seal_no", "ALTER TABLE `shipments` ADD COLUMN `seal_no` VARCHAR(120) NULL");
        await AddIndexIfMissingAsync(
            "shipments",
            "ux_shipment_container_load",
            "CREATE UNIQUE INDEX `ux_shipment_container_load` ON `shipments` (`container_load_id`)");

        _logger.LogInformation("Container confirmation and line-based finance schema is ready");
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
