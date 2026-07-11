using Futurem.Sourcing.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public sealed class OrderProductIndexUpgradeService
{
    private const string OldIndex = "ux_order_product_customer_barcode";
    private const string NewIndex = "ux_order_product_order_barcode";
    private readonly AppDbContext _db;

    public OrderProductIndexUpgradeService(AppDbContext db)
    {
        _db = db;
    }

    public async Task UpgradeAsync()
    {
        if (!_db.Database.IsRelational() || !await TableExistsAsync()) return;

        if (await IndexExistsAsync(OldIndex))
            await _db.Database.ExecuteSqlRawAsync($"DROP INDEX `{OldIndex}` ON `order_products`");

        if (!await IndexExistsAsync(NewIndex))
        {
            await _db.Database.ExecuteSqlRawAsync(
                $"CREATE UNIQUE INDEX `{NewIndex}` ON `order_products` (`source_customer_order_id`,`customer_barcode`)");
        }
    }

    private async Task<bool> TableExistsAsync()
    {
        var count = await _db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS `Value` FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'order_products'")
            .SingleAsync();
        return count > 0;
    }

    private async Task<bool> IndexExistsAsync(string indexName)
    {
        var count = await _db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS `Value` FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = 'order_products' AND index_name = {0}",
            indexName).SingleAsync();
        return count > 0;
    }
}
