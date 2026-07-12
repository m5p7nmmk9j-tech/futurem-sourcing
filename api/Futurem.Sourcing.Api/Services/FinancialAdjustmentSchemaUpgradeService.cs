using Futurem.Sourcing.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public sealed class FinancialAdjustmentSchemaUpgradeService
{
    private readonly AppDbContext _db;
    private readonly ILogger<FinancialAdjustmentSchemaUpgradeService> _logger;

    public FinancialAdjustmentSchemaUpgradeService(
        AppDbContext db,
        ILogger<FinancialAdjustmentSchemaUpgradeService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task UpgradeAsync()
    {
        if (!_db.Database.IsRelational()) return;

        await AddColumnIfMissingAsync("financial_adjustments", "finance_record_line_id", "ALTER TABLE `financial_adjustments` ADD COLUMN `finance_record_line_id` BIGINT NULL");
        await AddColumnIfMissingAsync("financial_adjustments", "shipment_id", "ALTER TABLE `financial_adjustments` ADD COLUMN `shipment_id` BIGINT NULL");
        await AddColumnIfMissingAsync("financial_adjustments", "shipment_expense_id", "ALTER TABLE `financial_adjustments` ADD COLUMN `shipment_expense_id` BIGINT NULL");
        await AddColumnIfMissingAsync("financial_adjustments", "source_type", "ALTER TABLE `financial_adjustments` ADD COLUMN `source_type` VARCHAR(80) NOT NULL DEFAULT ''");
        await AddColumnIfMissingAsync("financial_adjustments", "source_id", "ALTER TABLE `financial_adjustments` ADD COLUMN `source_id` BIGINT NULL");
        await AddColumnIfMissingAsync("financial_adjustments", "original_amount", "ALTER TABLE `financial_adjustments` ADD COLUMN `original_amount` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("financial_adjustments", "result_amount", "ALTER TABLE `financial_adjustments` ADD COLUMN `result_amount` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("financial_adjustments", "reason", "ALTER TABLE `financial_adjustments` ADD COLUMN `reason` TEXT NULL");
        await AddColumnIfMissingAsync("financial_adjustments", "approved_by", "ALTER TABLE `financial_adjustments` ADD COLUMN `approved_by` BIGINT NULL");
        await AddColumnIfMissingAsync("financial_adjustments", "approved_at", "ALTER TABLE `financial_adjustments` ADD COLUMN `approved_at` DATETIME NULL");
        await AddColumnIfMissingAsync("financial_adjustments", "applied_by", "ALTER TABLE `financial_adjustments` ADD COLUMN `applied_by` BIGINT NULL");
        await AddColumnIfMissingAsync("financial_adjustments", "applied_at", "ALTER TABLE `financial_adjustments` ADD COLUMN `applied_at` DATETIME NULL");
        await AddColumnIfMissingAsync("financial_adjustments", "cancelled_by", "ALTER TABLE `financial_adjustments` ADD COLUMN `cancelled_by` BIGINT NULL");
        await AddColumnIfMissingAsync("financial_adjustments", "cancelled_at", "ALTER TABLE `financial_adjustments` ADD COLUMN `cancelled_at` DATETIME NULL");
        await AddColumnIfMissingAsync("financial_adjustments", "cancel_reason", "ALTER TABLE `financial_adjustments` ADD COLUMN `cancel_reason` TEXT NULL");

        await _db.Database.ExecuteSqlRawAsync("UPDATE `financial_adjustments` SET `reason` = COALESCE(NULLIF(`reason`, ''), COALESCE(`remark`, '历史调整'))");
        await _db.Database.ExecuteSqlRawAsync("ALTER TABLE `financial_adjustments` MODIFY COLUMN `status` VARCHAR(40) NOT NULL DEFAULT 'draft'");
        await _db.Database.ExecuteSqlRawAsync("ALTER TABLE `financial_adjustments` MODIFY COLUMN `adjustment_type` VARCHAR(80) NOT NULL");
        await _db.Database.ExecuteSqlRawAsync("ALTER TABLE `financial_adjustments` MODIFY COLUMN `source_key` VARCHAR(255) NOT NULL");
        await _db.Database.ExecuteSqlRawAsync("ALTER TABLE `financial_adjustments` MODIFY COLUMN `source_type` VARCHAR(80) NOT NULL DEFAULT ''");
        await _db.Database.ExecuteSqlRawAsync("ALTER TABLE `financial_adjustments` MODIFY COLUMN `reason` TEXT NOT NULL");
        await _db.Database.ExecuteSqlRawAsync("ALTER TABLE `financial_adjustments` MODIFY COLUMN `qc_order_id` BIGINT NULL");
        await _db.Database.ExecuteSqlRawAsync("ALTER TABLE `financial_adjustments` MODIFY COLUMN `qc_order_line_id` BIGINT NULL");
        await _db.Database.ExecuteSqlRawAsync("ALTER TABLE `customer_advances` MODIFY COLUMN `source_payment_id` BIGINT NULL");
        await AddColumnIfMissingAsync("customer_advances", "source_adjustment_id", "ALTER TABLE `customer_advances` ADD COLUMN `source_adjustment_id` BIGINT NULL");

        await AddIndexIfMissingAsync("financial_adjustments", "ix_financial_adjustment_status", "CREATE INDEX `ix_financial_adjustment_status` ON `financial_adjustments` (`status`,`adjustment_type`)");
        await AddIndexIfMissingAsync("customer_advances", "ux_customer_advance_adjustment", "CREATE UNIQUE INDEX `ux_customer_advance_adjustment` ON `customer_advances` (`source_adjustment_id`)");

        _logger.LogInformation("Financial adjustment workflow schema is ready");
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
