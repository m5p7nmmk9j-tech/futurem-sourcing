using Futurem.Sourcing.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public sealed class DeliveryNoticeSchemaUpgradeService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DeliveryNoticeSchemaUpgradeService> _logger;

    public DeliveryNoticeSchemaUpgradeService(
        AppDbContext db,
        ILogger<DeliveryNoticeSchemaUpgradeService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task UpgradeAsync()
    {
        if (!_db.Database.IsRelational()) return;

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `delivery_notices` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `no` VARCHAR(80) NOT NULL,
              `source_key` VARCHAR(255) NOT NULL,
              `summary_order_id` BIGINT NOT NULL,
              `supplier_id` BIGINT NOT NULL,
              `warehouse_id` BIGINT NOT NULL,
              `planned_delivery_date` DATETIME NOT NULL,
              `status` VARCHAR(40) NOT NULL DEFAULT 'draft',
              `published_at` DATETIME NULL,
              `supplier_confirmed_at` DATETIME NULL,
              `total_quantity` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `total_cartons` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `received_quantity` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `received_cartons` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              UNIQUE KEY `ux_delivery_notice_source_key` (`source_key`),
              KEY `ix_delivery_notice_group` (`summary_order_id`,`supplier_id`,`warehouse_id`,`planned_delivery_date`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `delivery_notice_lines` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `delivery_notice_id` BIGINT NOT NULL,
              `summary_order_item_id` BIGINT NOT NULL,
              `purchase_order_id` BIGINT NOT NULL,
              `purchase_order_line_id` BIGINT NOT NULL,
              `order_product_id` BIGINT NOT NULL,
              `planned_quantity` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `planned_cartons` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `received_quantity` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `received_cartons` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              UNIQUE KEY `ux_delivery_notice_line_source` (`delivery_notice_id`,`summary_order_item_id`),
              KEY `ix_delivery_notice_line_summary_item` (`summary_order_item_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await AddColumnIfMissingAsync(
            "receiving_orders",
            "delivery_notice_id",
            "ALTER TABLE `receiving_orders` ADD COLUMN `delivery_notice_id` BIGINT NULL");
        await AddColumnIfMissingAsync(
            "receiving_orders",
            "warehouse_id",
            "ALTER TABLE `receiving_orders` ADD COLUMN `warehouse_id` BIGINT NULL");
        await AddColumnIfMissingAsync(
            "receiving_orders",
            "supplier_id",
            "ALTER TABLE `receiving_orders` ADD COLUMN `supplier_id` BIGINT NULL");
        await AddColumnIfMissingAsync(
            "receiving_orders",
            "temporary_quantity",
            "ALTER TABLE `receiving_orders` ADD COLUMN `temporary_quantity` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync(
            "receiving_orders",
            "temporary_cartons",
            "ALTER TABLE `receiving_orders` ADD COLUMN `temporary_cartons` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync(
            "document_lines",
            "delivery_notice_line_id",
            "ALTER TABLE `document_lines` ADD COLUMN `delivery_notice_line_id` BIGINT NULL");

        await AddIndexIfMissingAsync(
            "receiving_orders",
            "ix_receiving_delivery_notice",
            "CREATE INDEX `ix_receiving_delivery_notice` ON `receiving_orders` (`delivery_notice_id`)");
        await AddIndexIfMissingAsync(
            "document_lines",
            "ix_document_line_delivery_notice",
            "CREATE INDEX `ix_document_line_delivery_notice` ON `document_lines` (`delivery_notice_line_id`)");

        _logger.LogInformation("Delivery notice schema is ready");
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
