using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public class ShipmentExpenseService
{
    private static readonly (string Code, string Name, int SortNo)[] Defaults =
    [
        ("OCEAN_FREIGHT", "海运费", 10),
        ("WAREHOUSE_FEE", "仓库费", 20),
        ("HANDLING_FEE", "装卸费", 30),
        ("INLAND_FREIGHT", "内陆费", 40)
    ];

    private readonly AppDbContext _db;

    public ShipmentExpenseService(AppDbContext db)
    {
        _db = db;
    }

    public static string NormalizeName(string value)
        => string.Join(' ', (value ?? string.Empty).Trim().ToUpperInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public async Task EnsureDefaultsAsync(long shipmentId)
    {
        var shipment = await _db.Shipments.FindAsync(shipmentId);
        if (shipment == null) throw new KeyNotFoundException("Shipment not found");

        foreach (var item in Defaults)
        {
            var existing = await _db.ShipmentExpenses.IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.ShipmentId == shipmentId && x.ExpenseCode == item.Code);
            if (existing != null)
            {
                existing.IsDeleted = false;
                existing.ExpenseName = item.Name;
                existing.NormalizedExpenseName = NormalizeName(item.Name);
                existing.IsCustom = false;
                existing.Currency = shipment.Currency;
                existing.SortNo = item.SortNo;
                continue;
            }

            _db.ShipmentExpenses.Add(new ShipmentExpense
            {
                ShipmentId = shipmentId,
                ExpenseCode = item.Code,
                ExpenseName = item.Name,
                NormalizedExpenseName = NormalizeName(item.Name),
                IsCustom = false,
                Currency = shipment.Currency,
                SortNo = item.SortNo,
                CreatedAt = DateTime.Now
            });
        }

        await _db.SaveChangesAsync();
        await RecalculateExpenseTotalAsync(shipmentId);
    }

    public async Task ValidateAsync(Shipment shipment, ShipmentExpense expense, long? excludeId = null)
    {
        expense.ExpenseName = (expense.ExpenseName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(expense.ExpenseName))
            throw new InvalidOperationException("费用名称不能为空");

        expense.NormalizedExpenseName = NormalizeName(expense.ExpenseName);
        expense.Amount = FinanceBalanceService.Round2(expense.Amount);
        expense.Currency = shipment.Currency;

        if (expense.Amount < 0m)
            throw new InvalidOperationException("费用金额不能小于 0");
        if (expense.Amount > 0m && !expense.SupplierId.HasValue)
            throw new InvalidOperationException($"{expense.ExpenseName}金额大于 0，请选择供应商");

        if (expense.IsCustom)
        {
            expense.ExpenseCode = $"CUSTOM:{expense.NormalizedExpenseName}";
            var duplicate = await _db.ShipmentExpenses.AnyAsync(x =>
                x.ShipmentId == shipment.Id &&
                x.NormalizedExpenseName == expense.NormalizedExpenseName &&
                (!excludeId.HasValue || x.Id != excludeId.Value));
            if (duplicate) throw new InvalidOperationException("该费用名称已存在");
        }
        else
        {
            if (!Defaults.Any(x => x.Code == expense.ExpenseCode))
                throw new InvalidOperationException("固定费用类型无效");
            var fixedName = Defaults.First(x => x.Code == expense.ExpenseCode);
            expense.ExpenseName = fixedName.Name;
            expense.NormalizedExpenseName = NormalizeName(fixedName.Name);
            expense.SortNo = fixedName.SortNo;
        }
    }

    public async Task ValidateAllAsync(long shipmentId)
    {
        await EnsureDefaultsAsync(shipmentId);
        var shipment = await _db.Shipments.FindAsync(shipmentId);
        if (shipment == null) throw new KeyNotFoundException("Shipment not found");

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
        var shipment = await _db.Shipments.FindAsync(shipmentId);
        if (shipment == null) throw new KeyNotFoundException("Shipment not found");

        var total = await _db.ShipmentExpenses
            .Where(x => x.ShipmentId == shipmentId)
            .SumAsync(x => x.Amount);
        shipment.ExpenseTotal = FinanceBalanceService.Round2(total);
        shipment.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return shipment.ExpenseTotal;
    }
}
