using Futurem.Sourcing.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public sealed class QcConfirmationSchemaUpgradeService
{
    private readonly AppDbContext _db;
    private readonly ILogger<QcConfirmationSchemaUpgradeService> _logger;

    public QcConfirmationSchemaUpgradeService(
        AppDbContext db,
        ILogger<QcConfirmationSchemaUpgradeService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task UpgradeAsync()
    {
        if (!_db.Database.IsRelational()) return;

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `qc_order_lines` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `qc_order_id` BIGINT NOT NULL,
              `receiving_order_id` BIGINT NOT NULL,
              `receiving_line_id` BIGINT NOT NULL,
              `delivery_notice_line_id` BIGINT NULL,
              `purchase_order_id` BIGINT NOT NULL,
              `purchase_order_line_id` BIGINT NULL,
              `order_product_id` BIGINT NOT NULL,
              `supplier_id` BIGINT NOT NULL,
              `warehouse_id` BIGINT NULL,
              `confirmation_version` INT NOT NULL DEFAULT 0,
              `arrived_quantity` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `qualified_quantity` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `unqualified_quantity` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `returned_quantity` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `pending_quantity` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `accepted_quantity` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `purchase_unit_price` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `payable_amount` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              UNIQUE KEY `ux_qc_line_receiving_line` (`qc_order_id`,`receiving_line_id`),
              KEY `ix_qc_line_receiving` (`receiving_order_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `financial_adjustments` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `finance_record_id` BIGINT NOT NULL,
              `qc_order_id` BIGINT NOT NULL,
              `qc_order_line_id` BIGINT NOT NULL,
              `adjustment_type` VARCHAR(80) NOT NULL,
              `source_key` VARCHAR(255) NOT NULL,
              `status` VARCHAR(40) NOT NULL DEFAULT 'pending',
              `adjustment_date` DATETIME NOT NULL,
              `amount` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              UNIQUE KEY `ux_financial_adjustment_source` (`source_key`),
              KEY `ix_financial_adjustment_record` (`finance_record_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await AddColumnIfMissingAsync("qc_orders", "confirmation_version", "ALTER TABLE `qc_orders` ADD COLUMN `confirmation_version` INT NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("qc_orders", "confirmed_at", "ALTER TABLE `qc_orders` ADD COLUMN `confirmed_at` DATETIME NULL");
        await AddColumnIfMissingAsync("qc_orders", "unlocked_at", "ALTER TABLE `qc_orders` ADD COLUMN `unlocked_at` DATETIME NULL");
        await AddColumnIfMissingAsync("qc_orders", "unlocked_by", "ALTER TABLE `qc_orders` ADD COLUMN `unlocked_by` BIGINT NULL");
        await AddColumnIfMissingAsync("qc_orders", "unlock_reason", "ALTER TABLE `qc_orders` ADD COLUMN `unlock_reason` TEXT NULL");
        await AddColumnIfMissingAsync("finance_records", "qc_order_id", "ALTER TABLE `finance_records` ADD COLUMN `qc_order_id` BIGINT NULL");
        await AddColumnIfMissingAsync("finance_records", "qc_order_line_id", "ALTER TABLE `finance_records` ADD COLUMN `qc_order_line_id` BIGINT NULL");

        await AddIndexIfMissingAsync("qc_orders", "ux_qc_receiving_order", "CREATE UNIQUE INDEX `ux_qc_receiving_order` ON `qc_orders` (`receiving_order_id`)");
        await AddIndexIfMissingAsync("finance_records", "ix_finance_qc_line", "CREATE INDEX `ix_finance_qc_line` ON `finance_records` (`qc_order_line_id`)");
        await AddIndexIfMissingAsync("finance_records", "ix_finance_source_key", "CREATE INDEX `ix_finance_source_key` ON `finance_records` (`source_key`)");

        _logger.LogInformation("QC confirmation and payable schema is ready");
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
