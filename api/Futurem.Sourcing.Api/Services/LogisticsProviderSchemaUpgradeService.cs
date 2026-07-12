using Futurem.Sourcing.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public sealed class LogisticsProviderSchemaUpgradeService
{
    private readonly AppDbContext _db;
    private readonly ILogger<LogisticsProviderSchemaUpgradeService> _logger;

    public LogisticsProviderSchemaUpgradeService(AppDbContext db, ILogger<LogisticsProviderSchemaUpgradeService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task UpgradeAsync()
    {
        if (!_db.Database.IsRelational()) return;

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `logistics_providers` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `code` VARCHAR(80) NOT NULL,
              `name` VARCHAR(200) NOT NULL,
              `service_types_json` TEXT NOT NULL,
              `contact_name` VARCHAR(120) NULL,
              `phone` VARCHAR(80) NULL,
              `email` VARCHAR(200) NULL,
              `address` TEXT NULL,
              `tax_id` VARCHAR(120) NULL,
              `bank_info_json` TEXT NULL,
              `status` VARCHAR(40) NOT NULL DEFAULT 'active',
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              UNIQUE KEY `ux_logistics_provider_code` (`code`),
              KEY `ix_logistics_provider_status_name` (`status`,`name`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await AddColumnIfMissingAsync("shipment_expenses", "service_type", "ALTER TABLE `shipment_expenses` ADD COLUMN `service_type` VARCHAR(60) NOT NULL DEFAULT 'other_service'");
        await AddColumnIfMissingAsync("shipment_expenses", "logistics_provider_id", "ALTER TABLE `shipment_expenses` ADD COLUMN `logistics_provider_id` BIGINT NULL");
        await AddColumnIfMissingAsync("shipment_expenses", "provider_cost", "ALTER TABLE `shipment_expenses` ADD COLUMN `provider_cost` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("shipment_expenses", "customer_charge", "ALTER TABLE `shipment_expenses` ADD COLUMN `customer_charge` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("shipment_expenses", "profit_amount", "ALTER TABLE `shipment_expenses` ADD COLUMN `profit_amount` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("shipment_expenses", "needs_customer_charge_review", "ALTER TABLE `shipment_expenses` ADD COLUMN `needs_customer_charge_review` TINYINT(1) NOT NULL DEFAULT 0");
        await AddIndexIfMissingAsync("shipment_expenses", "ix_expense_provider_service", "CREATE INDEX `ix_expense_provider_service` ON `shipment_expenses` (`logistics_provider_id`,`service_type`)");

        await AddColumnIfMissingAsync("shipments", "customer_charge_total", "ALTER TABLE `shipments` ADD COLUMN `customer_charge_total` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("shipments", "logistics_profit_total", "ALTER TABLE `shipments` ADD COLUMN `logistics_profit_total` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("finance_records", "logistics_provider_id", "ALTER TABLE `finance_records` ADD COLUMN `logistics_provider_id` BIGINT NULL");
        await AddColumnIfMissingAsync("finance_records", "counterparty_type", "ALTER TABLE `finance_records` ADD COLUMN `counterparty_type` VARCHAR(40) NOT NULL DEFAULT ''");

        await _db.Database.ExecuteSqlRawAsync("""
            UPDATE `shipment_expenses`
               SET `provider_cost` = CASE WHEN `provider_cost` = 0 THEN `amount` ELSE `provider_cost` END,
                   `customer_charge` = CASE WHEN `customer_charge` = 0 THEN `amount` ELSE `customer_charge` END,
                   `profit_amount` = CASE WHEN `profit_amount` = 0 THEN
                       (CASE WHEN `customer_charge` = 0 THEN `amount` ELSE `customer_charge` END) -
                       (CASE WHEN `provider_cost` = 0 THEN `amount` ELSE `provider_cost` END)
                       ELSE `profit_amount` END,
                   `needs_customer_charge_review` = CASE WHEN `amount` > 0 AND `logistics_provider_id` IS NULL THEN 1 ELSE `needs_customer_charge_review` END,
                   `currency` = 'RMB'
            """);
        await _db.Database.ExecuteSqlRawAsync("UPDATE `shipments` SET `currency` = 'RMB'");

        _logger.LogInformation("Independent logistics provider and dual shipment expense schema is ready");
    }

    private async Task AddColumnIfMissingAsync(string table, string column, string alterSql)
    {
        var count = await _db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS `Value` FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = {0} AND column_name = {1}",
            table, column).SingleAsync();
        if (count == 0) await _db.Database.ExecuteSqlRawAsync(alterSql);
    }

    private async Task AddIndexIfMissingAsync(string table, string index, string createSql)
    {
        var count = await _db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS `Value` FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = {0} AND index_name = {1}",
            table, index).SingleAsync();
        if (count == 0) await _db.Database.ExecuteSqlRawAsync(createSql);
    }
}
