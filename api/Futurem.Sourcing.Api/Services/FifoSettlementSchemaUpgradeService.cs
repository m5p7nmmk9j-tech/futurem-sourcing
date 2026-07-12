using Futurem.Sourcing.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public sealed class FifoSettlementSchemaUpgradeService
{
    private readonly AppDbContext _db;
    private readonly ILogger<FifoSettlementSchemaUpgradeService> _logger;

    public FifoSettlementSchemaUpgradeService(AppDbContext db, ILogger<FifoSettlementSchemaUpgradeService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task UpgradeAsync()
    {
        if (!_db.Database.IsRelational()) return;

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `payment_allocations` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `payment_id` BIGINT NOT NULL,
              `finance_record_id` BIGINT NOT NULL,
              `finance_record_line_id` BIGINT NULL,
              `amount` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `sequence_no` INT NOT NULL DEFAULT 0,
              `allocation_type` VARCHAR(40) NOT NULL DEFAULT 'payment',
              `is_reversal` TINYINT(1) NOT NULL DEFAULT 0,
              `reverses_allocation_id` BIGINT NULL,
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              KEY `ix_payment_allocation_sequence` (`payment_id`,`sequence_no`),
              KEY `ix_payment_allocation_reversal` (`reverses_allocation_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `customer_advances` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `no` VARCHAR(100) NOT NULL,
              `customer_id` BIGINT NOT NULL,
              `source_payment_id` BIGINT NULL,
              `currency` VARCHAR(16) NOT NULL DEFAULT 'RMB',
              `original_amount` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `available_amount` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `status` VARCHAR(40) NOT NULL DEFAULT 'available',
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              UNIQUE KEY `ux_customer_advance_source_payment` (`source_payment_id`),
              KEY `ix_customer_advance_fifo` (`customer_id`,`status`,`created_at`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `customer_advance_usages` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `customer_advance_id` BIGINT NOT NULL,
              `finance_record_id` BIGINT NOT NULL,
              `finance_record_line_id` BIGINT NULL,
              `applied_amount` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `is_reversal` TINYINT(1) NOT NULL DEFAULT 0,
              `reverses_usage_id` BIGINT NULL,
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              KEY `ix_customer_advance_usage_record` (`customer_advance_id`,`finance_record_id`,`finance_record_line_id`,`is_reversal`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await AddColumnIfMissingAsync("payments", "counterparty_type", "ALTER TABLE `payments` ADD COLUMN `counterparty_type` VARCHAR(40) NOT NULL DEFAULT ''");
        await AddColumnIfMissingAsync("payments", "counterparty_id", "ALTER TABLE `payments` ADD COLUMN `counterparty_id` BIGINT NULL");
        await AddColumnIfMissingAsync("payments", "applied_amount", "ALTER TABLE `payments` ADD COLUMN `applied_amount` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("payments", "advance_amount", "ALTER TABLE `payments` ADD COLUMN `advance_amount` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("payments", "status", "ALTER TABLE `payments` ADD COLUMN `status` VARCHAR(40) NOT NULL DEFAULT 'draft'");
        await AddColumnIfMissingAsync("payments", "reversed_payment_id", "ALTER TABLE `payments` ADD COLUMN `reversed_payment_id` BIGINT NULL");
        await AddColumnIfMissingAsync("payments", "reversal_payment_id", "ALTER TABLE `payments` ADD COLUMN `reversal_payment_id` BIGINT NULL");
        await MakeColumnNullableAsync("payments", "finance_record_id", "ALTER TABLE `payments` MODIFY COLUMN `finance_record_id` BIGINT NULL");
        await AddIndexIfMissingAsync("payments", "ux_payment_reversed_payment", "CREATE UNIQUE INDEX `ux_payment_reversed_payment` ON `payments` (`reversed_payment_id`)");

        await AddColumnIfMissingAsync("supplier_prepayments", "logistics_provider_id", "ALTER TABLE `supplier_prepayments` ADD COLUMN `logistics_provider_id` BIGINT NULL");
        await AddColumnIfMissingAsync("supplier_prepayments", "counterparty_type", "ALTER TABLE `supplier_prepayments` ADD COLUMN `counterparty_type` VARCHAR(40) NOT NULL DEFAULT 'product_supplier'");
        await AddColumnIfMissingAsync("supplier_prepayments", "source_payment_id", "ALTER TABLE `supplier_prepayments` ADD COLUMN `source_payment_id` BIGINT NULL");
        await MakeColumnNullableAsync("supplier_prepayments", "supplier_id", "ALTER TABLE `supplier_prepayments` MODIFY COLUMN `supplier_id` BIGINT NULL");
        await AddIndexIfMissingAsync("supplier_prepayments", "ux_supplier_prepayment_source_payment", "CREATE UNIQUE INDEX `ux_supplier_prepayment_source_payment` ON `supplier_prepayments` (`source_payment_id`)");
        await AddIndexIfMissingAsync("supplier_prepayments", "ix_prepayment_counterparty_fifo", "CREATE INDEX `ix_prepayment_counterparty_fifo` ON `supplier_prepayments` (`counterparty_type`,`supplier_id`,`logistics_provider_id`,`status`(24),`created_at`)");

        await AddColumnIfMissingAsync("supplier_prepayment_usages", "finance_record_line_id", "ALTER TABLE `supplier_prepayment_usages` ADD COLUMN `finance_record_line_id` BIGINT NULL");
        await AddColumnIfMissingAsync("supplier_prepayment_usages", "is_reversal", "ALTER TABLE `supplier_prepayment_usages` ADD COLUMN `is_reversal` TINYINT(1) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("supplier_prepayment_usages", "reverses_usage_id", "ALTER TABLE `supplier_prepayment_usages` ADD COLUMN `reverses_usage_id` BIGINT NULL");

        await _db.Database.ExecuteSqlRawAsync("UPDATE `payments` SET `currency`='RMB' WHERE `currency` <> 'RMB' OR `currency` IS NULL");
        await _db.Database.ExecuteSqlRawAsync("UPDATE `supplier_prepayments` SET `currency`='RMB', `counterparty_type`='product_supplier' WHERE `counterparty_type`='' OR `counterparty_type` IS NULL");
        _logger.LogInformation("FIFO settlement, customer advance and separated supplier prepayment schema is ready");
    }

    private async Task AddColumnIfMissingAsync(string table, string column, string sql)
    {
        var count = await ColumnCountAsync(table, column);
        if (count == 0) await _db.Database.ExecuteSqlRawAsync(sql);
    }

    private async Task MakeColumnNullableAsync(string table, string column, string sql)
    {
        var nullable = await _db.Database.SqlQueryRaw<string>(
            "SELECT IS_NULLABLE AS `Value` FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name={0} AND column_name={1}",
            table, column).SingleOrDefaultAsync();
        if (string.Equals(nullable, "NO", StringComparison.OrdinalIgnoreCase))
            await _db.Database.ExecuteSqlRawAsync(sql);
    }

    private Task<int> ColumnCountAsync(string table, string column)
        => _db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS `Value` FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name={0} AND column_name={1}",
            table, column).SingleAsync();

    private async Task AddIndexIfMissingAsync(string table, string index, string sql)
    {
        var count = await _db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS `Value` FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name={0} AND index_name={1}",
            table, index).SingleAsync();
        if (count == 0) await _db.Database.ExecuteSqlRawAsync(sql);
    }
}
