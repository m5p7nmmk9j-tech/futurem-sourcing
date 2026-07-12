using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public class ShipmentExpenseService
{
    private static readonly (string Code, string Name, string ServiceType, int SortNo)[] Defaults =
    [
        ("OCEAN_FREIGHT", "海运费", "ocean_freight", 10),
        ("CUSTOMS", "报关费", "customs", 20),
        ("TRUCKING", "拖车费", "trucking", 30),
        ("WAREHOUSE_FEE", "仓储费", "warehouse", 40),
        ("COURIER", "快递费", "courier", 50),
        ("OTHER_SERVICE", "其他服务费", "other_service", 60)
    ];

    private readonly AppDbContext _db;

    public ShipmentExpenseService(AppDbContext db) => _db = db;

    public static string NormalizeName(string value)
        => string.Join(' ', (value ?? string.Empty).Trim().ToUpperInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public async Task EnsureDefaultsAsync(long shipmentId)
    {
        var shipment = await _db.Shipments.FindAsync(shipmentId)
            ?? throw new KeyNotFoundException("Shipment not found");

        foreach (var item in Defaults)
        {
            var existing = await _db.ShipmentExpenses.IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.ShipmentId == shipmentId && x.ExpenseCode == item.Code);
            if (existing is not null)
            {
                existing.IsDeleted = false;
                existing.ExpenseName = item.Name;
                existing.NormalizedExpenseName = NormalizeName(item.Name);
                existing.ServiceType = item.ServiceType;
                existing.IsCustom = false;
                existing.Currency = RmbMoneyService.Currency;
                existing.SortNo = item.SortNo;
                continue;
            }

            _db.ShipmentExpenses.Add(new ShipmentExpense
            {
                ShipmentId = shipmentId,
                ExpenseCode = item.Code,
                ExpenseName = item.Name,
                NormalizedExpenseName = NormalizeName(item.Name),
                ServiceType = item.ServiceType,
                IsCustom = false,
                Currency = RmbMoneyService.Currency,
                SortNo = item.SortNo,
                CreatedAt = DateTime.Now
            });
        }

        shipment.Currency = RmbMoneyService.Currency;
        await _db.SaveChangesAsync();
        await RecalculateExpenseTotalAsync(shipmentId);
    }

    public async Task ValidateAsync(Shipment shipment, ShipmentExpense expense, long? excludeId = null)
    {
        expense.ExpenseName = (expense.ExpenseName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(expense.ExpenseName))
            throw new BusinessRuleException("LOGISTICS_EXPENSE_NAME_REQUIRED", "费用名称不能为空");

        expense.NormalizedExpenseName = NormalizeName(expense.ExpenseName);
        if (expense.ProviderCost == 0m && expense.Amount > 0m)
            expense.ProviderCost = expense.Amount;
        expense.ProviderCost = RmbMoneyService.Round(expense.ProviderCost);
        expense.CustomerCharge = RmbMoneyService.Round(expense.CustomerCharge);
        expense.Amount = expense.ProviderCost;
        expense.ProfitAmount = RmbMoneyService.Round(expense.CustomerCharge - expense.ProviderCost);
        expense.Currency = RmbMoneyService.Currency;

        if (expense.ProviderCost < 0m || expense.CustomerCharge < 0m)
            throw new BusinessRuleException("LOGISTICS_AMOUNT_NEGATIVE", "服务商成本和客户收费不能小于0");

        if (expense.ProviderCost > 0m || expense.CustomerCharge > 0m)
        {
            if (!expense.LogisticsProviderId.HasValue)
                throw new BusinessRuleException("LOGISTICS_PROVIDER_REQUIRED", $"{expense.ExpenseName}有金额，请选择物流服务商");
            var providerExists = await _db.LogisticsProviders.AnyAsync(x =>
                x.Id == expense.LogisticsProviderId.Value && x.Status == "active");
            if (!providerExists)
                throw new BusinessRuleException("LOGISTICS_PROVIDER_NOT_FOUND", "物流服务商不存在或已停用");
        }
        else if (expense.LogisticsProviderId.HasValue)
        {
            var providerExists = await _db.LogisticsProviders.AnyAsync(x =>
                x.Id == expense.LogisticsProviderId.Value && x.Status == "active");
            if (!providerExists)
                throw new BusinessRuleException("LOGISTICS_PROVIDER_NOT_FOUND", "物流服务商不存在或已停用");
        }

        // New logistics expenses never use the product supplier master.
        expense.SupplierId = null;

        if (expense.IsCustom)
        {
            expense.ServiceType = string.IsNullOrWhiteSpace(expense.ServiceType) ? "other_service" : expense.ServiceType.Trim().ToLowerInvariant();
            expense.ExpenseCode = $"CUSTOM:{expense.NormalizedExpenseName}";
            var duplicate = await _db.ShipmentExpenses.AnyAsync(x =>
                x.ShipmentId == shipment.Id &&
                x.NormalizedExpenseName == expense.NormalizedExpenseName &&
                (!excludeId.HasValue || x.Id != excludeId.Value));
            if (duplicate)
                throw new BusinessRuleException("LOGISTICS_EXPENSE_DUPLICATE", "该费用名称已存在");
        }
        else
        {
            var fixedExpense = Defaults.FirstOrDefault(x => x.Code == expense.ExpenseCode);
            if (string.IsNullOrWhiteSpace(fixedExpense.Code))
                throw new BusinessRuleException("LOGISTICS_SERVICE_TYPE_INVALID", "固定服务类型无效");
            expense.ExpenseName = fixedExpense.Name;
            expense.NormalizedExpenseName = NormalizeName(fixedExpense.Name);
            expense.ServiceType = fixedExpense.ServiceType;
            expense.SortNo = fixedExpense.SortNo;
        }
    }

    public async Task ValidateAllAsync(long shipmentId)
    {
        await EnsureDefaultsAsync(shipmentId);
        var shipment = await _db.Shipments.FindAsync(shipmentId)
            ?? throw new KeyNotFoundException("Shipment not found");
        var expenses = await _db.ShipmentExpenses
            .Where(x => x.ShipmentId == shipmentId)
            .OrderBy(x => x.SortNo)
            .ThenBy(x => x.Id)
            .ToListAsync();

        foreach (var expense in expenses)
            await ValidateAsync(shipment, expense, expense.Id);

        await _db.SaveChangesAsync();
        await RecalculateExpenseTotalAsync(shipmentId);
    }

    public async Task<decimal> RecalculateExpenseTotalAsync(long shipmentId)
    {
        var shipment = await _db.Shipments.FindAsync(shipmentId)
            ?? throw new KeyNotFoundException("Shipment not found");
        var totals = await _db.ShipmentExpenses
            .Where(x => x.ShipmentId == shipmentId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                ProviderCost = group.Sum(x => x.ProviderCost),
                CustomerCharge = group.Sum(x => x.CustomerCharge),
                Profit = group.Sum(x => x.ProfitAmount)
            })
            .FirstOrDefaultAsync();

        shipment.ExpenseTotal = RmbMoneyService.Round(totals?.ProviderCost ?? 0m);
        shipment.CustomerChargeTotal = RmbMoneyService.Round(totals?.CustomerCharge ?? 0m);
        shipment.LogisticsProfitTotal = RmbMoneyService.Round(totals?.Profit ?? 0m);
        shipment.Currency = RmbMoneyService.Currency;
        shipment.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return shipment.ExpenseTotal;
    }
}
