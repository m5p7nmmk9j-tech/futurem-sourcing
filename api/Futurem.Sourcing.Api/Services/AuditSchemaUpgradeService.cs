using Futurem.Sourcing.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public sealed class AuditSchemaUpgradeService
{
    private readonly AppDbContext _db;

    public AuditSchemaUpgradeService(AppDbContext db)
    {
        _db = db;
    }

    public async Task UpgradeAsync()
    {
        if (!_db.Database.IsRelational()) return;

        await AddColumnIfMissingAsync(
            "reason",
            "ALTER TABLE `audit_logs` ADD COLUMN `reason` TEXT NULL");
        await AddColumnIfMissingAsync(
            "correlation_id",
            "ALTER TABLE `audit_logs` ADD COLUMN `correlation_id` VARCHAR(120) NULL");
        await AddColumnIfMissingAsync(
            "source_document_type",
            "ALTER TABLE `audit_logs` ADD COLUMN `source_document_type` VARCHAR(80) NULL");
        await AddColumnIfMissingAsync(
            "source_document_id",
            "ALTER TABLE `audit_logs` ADD COLUMN `source_document_id` BIGINT NULL");

        await _db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE `audit_logs` MODIFY COLUMN `correlation_id` VARCHAR(120) NULL");
        await _db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE `audit_logs` MODIFY COLUMN `source_document_type` VARCHAR(80) NULL");
    }

    private async Task AddColumnIfMissingAsync(string column, string sql)
    {
        var count = await _db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS `Value` FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'audit_logs' AND column_name = {0}",
            column).SingleAsync();
        if (count == 0) await _db.Database.ExecuteSqlRawAsync(sql);
    }
}
