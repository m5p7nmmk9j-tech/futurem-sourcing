using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public class DatabaseUpgradeService
{
    public const string TargetVersion = "1.1.0";
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
            await EnsureV1ProductImageColumnAsync();
            await EnsureV1ProductCodeColumnsAsync();
            await EnsureV1ProductIndexesAsync();
            await EnsureV1OrderTermsColumnsAsync();
            await EnsureShipmentFinanceSchemaAsync();
            await EnsureTwoDecimalPrecisionAsync();

            history = new MigrationHistory
            {
                MigrationName = "startup-auto-upgrade",
                Version = TargetVersion,
                StartedAt = DateTime.Now,
                Status = "running",
                CreatedAt = DateTime.Now
            };
            _db.MigrationHistories.Add(history);
            await _db.SaveChangesAsync();

            var oldVersions = await _db.SchemaVersions.Where(x => x.Status == "current").ToListAsync();
            foreach (var old in oldVersions) old.Status = "archived";
            _db.SchemaVersions.Add(new SchemaVersion
            {
                Version = TargetVersion,
                Status = "current",
                AppliedAt = DateTime.Now,
                Notes = "Shipment expenses, supplier payables and prepayments",
                CreatedAt = DateTime.Now
            });
            history.Status = "success";
            history.FinishedAt = DateTime.Now;
            history.Message = pending.Count == 0 ? "Database checked and upgraded." : $"Applied migrations: {string.Join(',', pending)}";
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            if (history is not null)
            {
                history.Status = "failed";
                history.FinishedAt = DateTime.Now;
                history.Message = ex.Message;
                try { await _db.SaveChangesAsync(); } catch { }
            }
            throw;
        }
    }

    private async Task EnsureShipmentFinanceSchemaAsync()
    {
        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `shipment_expenses` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `shipment_id` BIGINT NOT NULL,
              `expense_code` VARCHAR(120) NOT NULL,
              `expense_name` VARCHAR(200) NOT NULL,
              `normalized_expense_name` VARCHAR(200) NOT NULL,
              `is_custom` TINYINT(1) NOT NULL DEFAULT 0,
              `supplier_id` BIGINT NULL,
              `amount` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `currency` VARCHAR(20) NOT NULL DEFAULT 'RMB',
              `finance_record_id` BIGINT NULL,
              `finance_status` VARCHAR(40) NOT NULL DEFAULT 'pending',
              `sort_no` INT NOT NULL DEFAULT 0,
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              UNIQUE KEY `ux_shipment_expense_code` (`shipment_id`,`expense_code`),
              UNIQUE KEY `ux_shipment_expense_name` (`shipment_id`,`normalized_expense_name`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `supplier_prepayments` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `no` VARCHAR(80) NOT NULL,
              `supplier_id` BIGINT NOT NULL,
              `currency` VARCHAR(20) NOT NULL,
              `original_amount` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `available_amount` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `source_type` VARCHAR(80) NOT NULL,
              `source_id` BIGINT NOT NULL,
              `source_finance_record_id` BIGINT NULL,
              `status` VARCHAR(40) NOT NULL DEFAULT 'available',
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              KEY `ix_supplier_prepayment_available` (`supplier_id`,`currency`,`status`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `supplier_prepayment_usages` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `supplier_prepayment_id` BIGINT NOT NULL,
              `finance_record_id` BIGINT NOT NULL,
              `amount` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `usage_type` VARCHAR(40) NOT NULL DEFAULT 'apply',
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              KEY `ix_prepayment_usage_source` (`supplier_prepayment_id`,`finance_record_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await AddColumnIfMissingAsync("shipments", "currency", "ALTER TABLE `shipments` ADD COLUMN `currency` VARCHAR(20) NOT NULL DEFAULT 'RMB'");
        await AddColumnIfMissingAsync("shipments", "calculated_total_cbm", "ALTER TABLE `shipments` ADD COLUMN `calculated_total_cbm` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("shipments", "final_total_cbm", "ALTER TABLE `shipments` ADD COLUMN `final_total_cbm` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("shipments", "calculated_gross_weight_kg", "ALTER TABLE `shipments` ADD COLUMN `calculated_gross_weight_kg` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("shipments", "final_gross_weight_kg", "ALTER TABLE `shipments` ADD COLUMN `final_gross_weight_kg` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("shipments", "calculated_net_weight_kg", "ALTER TABLE `shipments` ADD COLUMN `calculated_net_weight_kg` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("shipments", "final_net_weight_kg", "ALTER TABLE `shipments` ADD COLUMN `final_net_weight_kg` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("shipments", "expense_total", "ALTER TABLE `shipments` ADD COLUMN `expense_total` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("shipments", "finance_sync_status", "ALTER TABLE `shipments` ADD COLUMN `finance_sync_status` VARCHAR(40) NOT NULL DEFAULT 'not_synced'");
        await AddColumnIfMissingAsync("shipments", "finance_sync_message", "ALTER TABLE `shipments` ADD COLUMN `finance_sync_message` TEXT NULL");
        await AddColumnIfMissingAsync("shipments", "finance_synced_at", "ALTER TABLE `shipments` ADD COLUMN `finance_synced_at` DATETIME NULL");

        await AddColumnIfMissingAsync("finance_records", "shipment_expense_id", "ALTER TABLE `finance_records` ADD COLUMN `shipment_expense_id` BIGINT NULL");
        await AddColumnIfMissingAsync("finance_records", "prepayment_applied_amount", "ALTER TABLE `finance_records` ADD COLUMN `prepayment_applied_amount` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("finance_records", "overpayment_transferred_amount", "ALTER TABLE `finance_records` ADD COLUMN `overpayment_transferred_amount` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("finance_records", "source_key", "ALTER TABLE `finance_records` ADD COLUMN `source_key` VARCHAR(200) NULL");
    }

    private async Task EnsureTwoDecimalPrecisionAsync()
    {
        var sql = new[]
        {
            "ALTER TABLE `products` MODIFY COLUMN `purchase_price` DECIMAL(18,2) NOT NULL DEFAULT 0",
            "ALTER TABLE `products` MODIFY COLUMN `carton_qty` DECIMAL(18,2) NOT NULL DEFAULT 0",
            "ALTER TABLE `products` MODIFY COLUMN `carton_length_cm` DECIMAL(18,2) NOT NULL DEFAULT 0",
            "ALTER TABLE `products` MODIFY COLUMN `carton_width_cm` DECIMAL(18,2) NOT NULL DEFAULT 0",
            "ALTER TABLE `products` MODIFY COLUMN `carton_height_cm` DECIMAL(18,2) NOT NULL DEFAULT 0",
            "ALTER TABLE `products` MODIFY COLUMN `carton_gw_kg` DECIMAL(18,2) NOT NULL DEFAULT 0",
            "ALTER TABLE `products` MODIFY COLUMN `carton_nw_kg` DECIMAL(18,2) NOT NULL DEFAULT 0",
            "ALTER TABLE `document_lines` MODIFY COLUMN `quantity` DECIMAL(18,2) NOT NULL DEFAULT 0",
            "ALTER TABLE `document_lines` MODIFY COLUMN `unit_price` DECIMAL(18,2) NOT NULL DEFAULT 0",
            "ALTER TABLE `document_lines` MODIFY COLUMN `amount` DECIMAL(18,2) NOT NULL DEFAULT 0",
            "ALTER TABLE `document_lines` MODIFY COLUMN `carton_qty` DECIMAL(18,2) NOT NULL DEFAULT 0",
            "ALTER TABLE `document_lines` MODIFY COLUMN `cartons` DECIMAL(18,2) NOT NULL DEFAULT 0",
            "ALTER TABLE `document_lines` MODIFY COLUMN `carton_cbm` DECIMAL(18,2) NOT NULL DEFAULT 0",
            "ALTER TABLE `document_lines` MODIFY COLUMN `total_cbm` DECIMAL(18,2) NOT NULL DEFAULT 0",
            "ALTER TABLE `document_lines` MODIFY COLUMN `total_gw_kg` DECIMAL(18,2) NOT NULL DEFAULT 0",
            "ALTER TABLE `document_lines` MODIFY COLUMN `total_nw_kg` DECIMAL(18,2) NOT NULL DEFAULT 0",
            "ALTER TABLE `finance_records` MODIFY COLUMN `amount` DECIMAL(18,2) NOT NULL DEFAULT 0",
            "ALTER TABLE `finance_records` MODIFY COLUMN `paid_amount` DECIMAL(18,2) NOT NULL DEFAULT 0",
            "ALTER TABLE `payments` MODIFY COLUMN `amount` DECIMAL(18,2) NOT NULL DEFAULT 0",
            "ALTER TABLE `payments` MODIFY COLUMN `fee_amount` DECIMAL(18,2) NOT NULL DEFAULT 0"
        };

        foreach (var statement in sql)
            await _db.Database.ExecuteSqlRawAsync(statement);
    }

    private async Task EnsureV1ProductColumnsAsync()
    {
        await AddColumnIfMissingAsync("products", "purchase_price", "ALTER TABLE `products` ADD COLUMN `purchase_price` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("products", "carton_qty", "ALTER TABLE `products` ADD COLUMN `carton_qty` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("products", "carton_length_cm", "ALTER TABLE `products` ADD COLUMN `carton_length_cm` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("products", "carton_width_cm", "ALTER TABLE `products` ADD COLUMN `carton_width_cm` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("products", "carton_height_cm", "ALTER TABLE `products` ADD COLUMN `carton_height_cm` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("products", "carton_gw_kg", "ALTER TABLE `products` ADD COLUMN `carton_gw_kg` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("products", "carton_nw_kg", "ALTER TABLE `products` ADD COLUMN `carton_nw_kg` DECIMAL(18,2) NOT NULL DEFAULT 0");
    }

    private async Task EnsureV1OrderTermsColumnsAsync()
    {
        await AddColumnIfMissingAsync("customer_orders", "expected_delivery_date", "ALTER TABLE `customer_orders` ADD COLUMN `expected_delivery_date` DATETIME NULL");
        await AddColumnIfMissingAsync("customer_orders", "delivery_terms", "ALTER TABLE `customer_orders` ADD COLUMN `delivery_terms` VARCHAR(500) NULL");
        await AddColumnIfMissingAsync("customer_orders", "payment_terms", "ALTER TABLE `customer_orders` ADD COLUMN `payment_terms` VARCHAR(500) NULL");
        await AddColumnIfMissingAsync("purchase_orders", "delivery_terms", "ALTER TABLE `purchase_orders` ADD COLUMN `delivery_terms` VARCHAR(500) NULL");
        await AddColumnIfMissingAsync("purchase_orders", "payment_terms", "ALTER TABLE `purchase_orders` ADD COLUMN `payment_terms` VARCHAR(500) NULL");
    }

    private async Task EnsureV1ProductImageColumnAsync()
    {
        await _db.Database.ExecuteSqlRawAsync("ALTER TABLE `products` MODIFY COLUMN `image_url` MEDIUMTEXT NULL");
    }

    private async Task AddColumnIfMissingAsync(string table, string column, string alterSql)
    {
        var exists = await _db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS `Value` FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = {0} AND column_name = {1}", table, column)
            .SingleAsync();
        if (exists > 0) return;
        await _db.Database.ExecuteSqlRawAsync(alterSql);
    }

    private async Task EnsureV1ProductCodeColumnsAsync()
    {
        await _db.Database.ExecuteSqlRawAsync("ALTER TABLE `products` MODIFY COLUMN `sku` VARCHAR(80) NOT NULL");
        await _db.Database.ExecuteSqlRawAsync("ALTER TABLE `products` MODIFY COLUMN `barcode` VARCHAR(80) NOT NULL");
    }

    private async Task EnsureV1ProductIndexesAsync()
    {
        await AddUniqueIndexIfMissingAsync(
            "products",
            "barcode",
            "idx_products_barcode_unique",
            "SELECT COUNT(*) AS `Value` FROM (SELECT `barcode` FROM `products` WHERE `barcode` IS NOT NULL AND `barcode` <> '' GROUP BY `barcode` HAVING COUNT(*) > 1) duplicated_values",
            "CREATE UNIQUE INDEX `idx_products_barcode_unique` ON `products` (`barcode`)");
        await AddUniqueIndexIfMissingAsync(
            "products",
            "sku",
            "idx_products_sku_unique",
            "SELECT COUNT(*) AS `Value` FROM (SELECT `sku` FROM `products` WHERE `sku` IS NOT NULL AND `sku` <> '' GROUP BY `sku` HAVING COUNT(*) > 1) duplicated_values",
            "CREATE UNIQUE INDEX `idx_products_sku_unique` ON `products` (`sku`)");
    }

    private async Task AddUniqueIndexIfMissingAsync(string table, string column, string indexName, string duplicateSql, string createIndexSql)
    {
        var exists = await _db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS `Value` FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = {0} AND index_name = {1}", table, indexName)
            .SingleAsync();
        if (exists > 0) return;

        var duplicates = await _db.Database.SqlQueryRaw<int>(duplicateSql).SingleAsync();
        if (duplicates > 0)
        {
            _logger.LogWarning("Skipped unique index {IndexName} because duplicate {Column} values exist in {Table}", indexName, column, table);
            return;
        }

        await _db.Database.ExecuteSqlRawAsync(createIndexSql);
    }
}
