using Futurem.Sourcing.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public sealed class OrderProductSchemaUpgradeService
{
    public const string TargetVersion = "2.0.0";

    private readonly AppDbContext _db;
    private readonly ILogger<OrderProductSchemaUpgradeService> _logger;

    public OrderProductSchemaUpgradeService(
        AppDbContext db,
        ILogger<OrderProductSchemaUpgradeService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task UpgradeAsync()
    {
        if (!_db.Database.IsRelational()) return;

        await CreateOrderProductTablesAsync();
        await ExtendOrderHeadersAsync();
        await ExtendDocumentLinesAsync();
        await ExtendPrintTemplatesAsync();
        await ExtendAuditLogsAsync();
        await NormalizeRmbAsync();
        await EnsureIndexesAsync();

        _logger.LogInformation("Order product schema upgraded to {Version}", TargetVersion);
    }

    private async Task CreateOrderProductTablesAsync()
    {
        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `order_products` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `customer_id` BIGINT NOT NULL,
              `supplier_id` BIGINT NOT NULL,
              `source_order_product_id` BIGINT NULL,
              `source_customer_order_id` BIGINT NOT NULL,
              `system_sku` VARCHAR(80) NOT NULL,
              `customer_item_no` VARCHAR(120) NULL,
              `customer_barcode` VARCHAR(120) NOT NULL,
              `supplier_item_no` VARCHAR(120) NULL,
              `name_cn` VARCHAR(500) NOT NULL,
              `name_en` VARCHAR(500) NULL,
              `name_es` VARCHAR(500) NULL,
              `specification` VARCHAR(500) NULL,
              `color` VARCHAR(200) NULL,
              `unit` VARCHAR(40) NOT NULL DEFAULT 'PCS',
              `purchase_unit_price` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `sales_unit_price` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `carton_qty` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `carton_length_cm` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `carton_width_cm` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `carton_height_cm` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `carton_cbm` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `carton_gw_kg` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `carton_nw_kg` DECIMAL(18,2) NOT NULL DEFAULT 0,
              `importer_profile_id` BIGINT NOT NULL,
              `importer_snapshot_json` MEDIUMTEXT NOT NULL,
              `label_template_id` BIGINT NOT NULL,
              `label_template_snapshot_json` MEDIUMTEXT NOT NULL,
              `mark_template_id` BIGINT NOT NULL,
              `mark_template_snapshot_json` MEDIUMTEXT NOT NULL,
              `batch_code` VARCHAR(20) NOT NULL DEFAULT '',
              `status` VARCHAR(40) NOT NULL DEFAULT 'draft',
              `locked_at` DATETIME NULL,
              `needs_review` TINYINT(1) NOT NULL DEFAULT 0,
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              UNIQUE KEY `ux_order_product_customer_barcode` (`customer_id`,`customer_barcode`),
              KEY `ix_order_product_source_status` (`source_customer_order_id`,`status`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `order_product_images` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `order_product_id` BIGINT NOT NULL,
              `image_url` MEDIUMTEXT NOT NULL,
              `image_type` VARCHAR(40) NOT NULL DEFAULT 'detail',
              `sort_no` INT NOT NULL DEFAULT 0,
              `file_name` VARCHAR(500) NULL,
              `content_type` VARCHAR(120) NULL,
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              KEY `ix_order_product_image_type` (`order_product_id`,`image_type`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `customer_importer_profiles` (
              `id` BIGINT NOT NULL AUTO_INCREMENT,
              `customer_id` BIGINT NOT NULL,
              `name` VARCHAR(200) NOT NULL,
              `company_name` VARCHAR(500) NOT NULL,
              `tax_id_or_rfc` VARCHAR(120) NULL,
              `address` TEXT NOT NULL,
              `contact_name` VARCHAR(200) NULL,
              `phone` VARCHAR(120) NULL,
              `email` VARCHAR(320) NULL,
              `logo_url` MEDIUMTEXT NULL,
              `default_origin_text` VARCHAR(200) NOT NULL DEFAULT 'Made in China',
              `default_label_template_id` BIGINT NULL,
              `default_mark_template_id` BIGINT NULL,
              `is_default` TINYINT(1) NOT NULL DEFAULT 0,
              `status` VARCHAR(40) NOT NULL DEFAULT 'active',
              `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
              `created_by` BIGINT NULL,
              `created_at` DATETIME NOT NULL,
              `updated_by` BIGINT NULL,
              `updated_at` DATETIME NULL,
              `remark` TEXT NULL,
              PRIMARY KEY (`id`),
              KEY `ix_importer_customer_status_default` (`customer_id`,`status`,`is_default`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """);
    }

    private async Task ExtendOrderHeadersAsync()
    {
        foreach (var table in new[] { "customer_orders", "purchase_orders" })
        {
            await AddColumnIfMissingAsync(table, "importer_profile_id", $"ALTER TABLE `{table}` ADD COLUMN `importer_profile_id` BIGINT NULL");
            await AddColumnIfMissingAsync(table, "label_template_id", $"ALTER TABLE `{table}` ADD COLUMN `label_template_id` BIGINT NULL");
            await AddColumnIfMissingAsync(table, "mark_template_id", $"ALTER TABLE `{table}` ADD COLUMN `mark_template_id` BIGINT NULL");
            await AddColumnIfMissingAsync(table, "importer_snapshot_json", $"ALTER TABLE `{table}` ADD COLUMN `importer_snapshot_json` MEDIUMTEXT NULL");
            await AddColumnIfMissingAsync(table, "label_template_snapshot_json", $"ALTER TABLE `{table}` ADD COLUMN `label_template_snapshot_json` MEDIUMTEXT NULL");
            await AddColumnIfMissingAsync(table, "mark_template_snapshot_json", $"ALTER TABLE `{table}` ADD COLUMN `mark_template_snapshot_json` MEDIUMTEXT NULL");
            await AddColumnIfMissingAsync(table, "confirmed_at", $"ALTER TABLE `{table}` ADD COLUMN `confirmed_at` DATETIME NULL");

            await _db.Database.ExecuteSqlRawAsync(
                $"UPDATE `{table}` SET `currency` = 'RMB', " +
                "`importer_snapshot_json` = COALESCE(`importer_snapshot_json`, '{{}}'), " +
                "`label_template_snapshot_json` = COALESCE(`label_template_snapshot_json`, '{{}}'), " +
                "`mark_template_snapshot_json` = COALESCE(`mark_template_snapshot_json`, '{{}}')");
        }
    }

    private async Task ExtendDocumentLinesAsync()
    {
        await AddColumnIfMissingAsync("document_lines", "order_product_id", "ALTER TABLE `document_lines` ADD COLUMN `order_product_id` BIGINT NULL");
        await AddColumnIfMissingAsync("document_lines", "source_document_line_id", "ALTER TABLE `document_lines` ADD COLUMN `source_document_line_id` BIGINT NULL");
        await AddColumnIfMissingAsync("document_lines", "customer_id", "ALTER TABLE `document_lines` ADD COLUMN `customer_id` BIGINT NULL");
        await AddColumnIfMissingAsync("document_lines", "supplier_id", "ALTER TABLE `document_lines` ADD COLUMN `supplier_id` BIGINT NULL");
        await AddColumnIfMissingAsync("document_lines", "purchase_unit_price_snapshot", "ALTER TABLE `document_lines` ADD COLUMN `purchase_unit_price_snapshot` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("document_lines", "sales_unit_price_snapshot", "ALTER TABLE `document_lines` ADD COLUMN `sales_unit_price_snapshot` DECIMAL(18,2) NOT NULL DEFAULT 0");
        await AddColumnIfMissingAsync("document_lines", "warehouse_id", "ALTER TABLE `document_lines` ADD COLUMN `warehouse_id` BIGINT NULL");
        await AddColumnIfMissingAsync("document_lines", "warehouse_location_id", "ALTER TABLE `document_lines` ADD COLUMN `warehouse_location_id` BIGINT NULL");
        await AddColumnIfMissingAsync("document_lines", "inventory_lot_id", "ALTER TABLE `document_lines` ADD COLUMN `inventory_lot_id` BIGINT NULL");
    }

    private async Task ExtendPrintTemplatesAsync()
    {
        await AddColumnIfMissingAsync("print_templates", "template_type", "ALTER TABLE `print_templates` ADD COLUMN `template_type` VARCHAR(40) NOT NULL DEFAULT ''");
        await AddColumnIfMissingAsync("print_templates", "customer_id", "ALTER TABLE `print_templates` ADD COLUMN `customer_id` BIGINT NULL");
        await AddColumnIfMissingAsync("print_templates", "importer_profile_id", "ALTER TABLE `print_templates` ADD COLUMN `importer_profile_id` BIGINT NULL");
        await AddColumnIfMissingAsync("print_templates", "designer_mode", "ALTER TABLE `print_templates` ADD COLUMN `designer_mode` VARCHAR(40) NOT NULL DEFAULT 'fixed'");
        await AddColumnIfMissingAsync("print_templates", "paper_width_mm", "ALTER TABLE `print_templates` ADD COLUMN `paper_width_mm` DECIMAL(18,2) NULL");
        await AddColumnIfMissingAsync("print_templates", "paper_height_mm", "ALTER TABLE `print_templates` ADD COLUMN `paper_height_mm` DECIMAL(18,2) NULL");
        await AddColumnIfMissingAsync("print_templates", "orientation", "ALTER TABLE `print_templates` ADD COLUMN `orientation` VARCHAR(40) NOT NULL DEFAULT 'portrait'");
        await AddColumnIfMissingAsync("print_templates", "layout_json", "ALTER TABLE `print_templates` ADD COLUMN `layout_json` MEDIUMTEXT NULL");
        await _db.Database.ExecuteSqlRawAsync("UPDATE `print_templates` SET `layout_json` = COALESCE(`layout_json`, '{{}}')");
    }

    private async Task ExtendAuditLogsAsync()
    {
        await AddColumnIfMissingAsync("audit_logs", "reason", "ALTER TABLE `audit_logs` ADD COLUMN `reason` TEXT NULL");
        await AddColumnIfMissingAsync("audit_logs", "correlation_id", "ALTER TABLE `audit_logs` ADD COLUMN `correlation_id` VARCHAR(120) NULL");
        await AddColumnIfMissingAsync("audit_logs", "source_document_type", "ALTER TABLE `audit_logs` ADD COLUMN `source_document_type` VARCHAR(80) NULL");
        await AddColumnIfMissingAsync("audit_logs", "source_document_id", "ALTER TABLE `audit_logs` ADD COLUMN `source_document_id` BIGINT NULL");
    }

    private async Task NormalizeRmbAsync()
    {
        foreach (var table in new[]
        {
            "customers", "summary_orders", "shipments", "shipment_expenses",
            "finance_records", "supplier_prepayments"
        })
        {
            if (await TableExistsAsync(table))
                await _db.Database.ExecuteSqlRawAsync($"UPDATE `{table}` SET `currency` = 'RMB'");
        }
    }

    private async Task EnsureIndexesAsync()
    {
        await AddIndexIfMissingAsync("document_lines", "ix_document_lines_order_product", "CREATE INDEX `ix_document_lines_order_product` ON `document_lines` (`order_product_id`)");
        await AddIndexIfMissingAsync("print_templates", "ix_print_template_customer_type", "CREATE INDEX `ix_print_template_customer_type` ON `print_templates` (`customer_id`,`template_type`,`status`)");
        await AddIndexIfMissingAsync("audit_logs", "ix_audit_correlation", "CREATE INDEX `ix_audit_correlation` ON `audit_logs` (`correlation_id`)");
    }

    private async Task<bool> TableExistsAsync(string table)
    {
        var count = await _db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS `Value` FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = {0}",
            table).SingleAsync();
        return count > 0;
    }

    private async Task AddColumnIfMissingAsync(string table, string column, string alterSql)
    {
        var count = await _db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS `Value` FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = {0} AND column_name = {1}",
            table,
            column).SingleAsync();
        if (count == 0) await _db.Database.ExecuteSqlRawAsync(alterSql);
    }

    private async Task AddIndexIfMissingAsync(string table, string indexName, string createSql)
    {
        var count = await _db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS `Value` FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = {0} AND index_name = {1}",
            table,
            indexName).SingleAsync();
        if (count == 0) await _db.Database.ExecuteSqlRawAsync(createSql);
    }
}
