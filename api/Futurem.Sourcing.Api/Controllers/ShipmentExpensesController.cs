using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/shipments/{shipmentId:long}/expenses")]
public class ShipmentExpensesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ShipmentExpenseService _expenseService;

    public ShipmentExpensesController(AppDbContext db, ShipmentExpenseService expenseService)
    {
        _db = db;
        _expenseService = expenseService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> List(long shipmentId)
    {
        var shipment = await _db.Shipments.FindAsync(shipmentId);
        if (shipment == null) return NotFound();
        await _expenseService.EnsureDefaultsAsync(shipmentId);

        var expenses = await _db.ShipmentExpenses
            .Where(x => x.ShipmentId == shipmentId)
            .OrderBy(x => x.SortNo)
            .ThenBy(x => x.Id)
            .ToListAsync();
        var providerIds = expenses.Where(x => x.LogisticsProviderId.HasValue)
            .Select(x => x.LogisticsProviderId!.Value).Distinct().ToList();
        var providers = await _db.LogisticsProviders
            .Where(x => providerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);
        var financeIds = expenses.Where(x => x.FinanceRecordId.HasValue)
            .Select(x => x.FinanceRecordId!.Value).ToList();
        var finances = await _db.FinanceRecords
            .Where(x => financeIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        return expenses.Select(expense =>
        {
            LogisticsProvider? provider = null;
            if (expense.LogisticsProviderId.HasValue)
                providers.TryGetValue(expense.LogisticsProviderId.Value, out provider);
            FinanceRecord? finance = null;
            if (expense.FinanceRecordId.HasValue)
                finances.TryGetValue(expense.FinanceRecordId.Value, out finance);
            return (object)new
            {
                expense.Id,
                expense.ShipmentId,
                expense.ExpenseCode,
                expense.ExpenseName,
                expense.ServiceType,
                expense.IsCustom,
                expense.LogisticsProviderId,
                logisticsProviderName = provider?.Name,
                expense.ProviderCost,
                expense.CustomerCharge,
                expense.ProfitAmount,
                amount = expense.ProviderCost,
                currency = RmbMoneyService.Currency,
                expense.NeedsCustomerChargeReview,
                expense.FinanceRecordId,
                financeNo = finance?.No,
                paidAmount = finance?.PaidAmount ?? 0m,
                outstandingAmount = finance == null ? expense.ProviderCost : FinanceBalanceService.Outstanding(finance),
                financeStatus = finance?.Status ?? expense.FinanceStatus,
                expense.Remark,
                expense.SortNo
            };
        }).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<ShipmentExpense>> Create(long shipmentId, ShipmentExpense input)
    {
        var shipment = await RequireEditableShipmentAsync(shipmentId);
        if (!input.IsCustom)
            throw new BusinessRuleException("FIXED_LOGISTICS_EXPENSE_SYSTEM_MANAGED", "固定服务费用由系统自动创建");

        await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;
        try
        {
            input.Id = 0;
            input.ShipmentId = shipmentId;
            input.IsCustom = true;
            input.CreatedAt = DateTime.Now;
            await _expenseService.ValidateAsync(shipment, input);

            var deleted = await _db.ShipmentExpenses.IgnoreQueryFilters().FirstOrDefaultAsync(x =>
                x.ShipmentId == shipmentId && x.ExpenseCode == input.ExpenseCode && x.IsDeleted);
            if (deleted is not null)
            {
                deleted.IsDeleted = false;
                ApplyEditableValues(deleted, input);
                deleted.UpdatedAt = DateTime.Now;
                input = deleted;
            }
            else
            {
                var nextSort = await _db.ShipmentExpenses
                    .Where(x => x.ShipmentId == shipmentId)
                    .MaxAsync(x => (int?)x.SortNo) ?? 60;
                input.SortNo = nextSort + 10;
                _db.ShipmentExpenses.Add(input);
            }

            await _db.SaveChangesAsync();
            await _expenseService.RecalculateExpenseTotalAsync(shipmentId);
            if (transaction is not null) await transaction.CommitAsync();
            return input;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpPut("{expenseId:long}")]
    public async Task<ActionResult<ShipmentExpense>> Update(long shipmentId, long expenseId, ShipmentExpense input)
    {
        var shipment = await RequireEditableShipmentAsync(shipmentId);
        var entity = await _db.ShipmentExpenses
            .FirstOrDefaultAsync(x => x.Id == expenseId && x.ShipmentId == shipmentId);
        if (entity == null) return NotFound();

        await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;
        try
        {
            entity.ExpenseName = entity.IsCustom ? input.ExpenseName : entity.ExpenseName;
            entity.ServiceType = input.ServiceType;
            entity.LogisticsProviderId = input.LogisticsProviderId;
            entity.ProviderCost = input.ProviderCost;
            entity.CustomerCharge = input.CustomerCharge;
            entity.Amount = input.ProviderCost;
            entity.NeedsCustomerChargeReview = false;
            entity.Remark = input.Remark;
            entity.UpdatedAt = DateTime.Now;
            await _expenseService.ValidateAsync(shipment, entity, entity.Id);
            await _db.SaveChangesAsync();
            await _expenseService.RecalculateExpenseTotalAsync(shipmentId);
            if (transaction is not null) await transaction.CommitAsync();
            return entity;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpDelete("{expenseId:long}")]
    public async Task<IActionResult> Delete(long shipmentId, long expenseId)
    {
        await RequireEditableShipmentAsync(shipmentId);
        var entity = await _db.ShipmentExpenses
            .FirstOrDefaultAsync(x => x.Id == expenseId && x.ShipmentId == shipmentId);
        if (entity == null) return NotFound();

        entity.ProviderCost = 0m;
        entity.CustomerCharge = 0m;
        entity.ProfitAmount = 0m;
        entity.Amount = 0m;
        entity.LogisticsProviderId = null;
        entity.SupplierId = null;
        entity.NeedsCustomerChargeReview = false;
        entity.UpdatedAt = DateTime.Now;
        if (entity.IsCustom) entity.IsDeleted = true;
        await _db.SaveChangesAsync();
        await _expenseService.RecalculateExpenseTotalAsync(shipmentId);
        return Ok(new { ok = true });
    }

    private async Task<Shipment> RequireEditableShipmentAsync(long shipmentId)
    {
        var shipment = await _db.Shipments.FindAsync(shipmentId)
            ?? throw new KeyNotFoundException("出运单不存在");
        if (shipment.Status != "draft")
            throw new BusinessRuleException("SHIPMENT_EXPENSE_LOCKED", "出运单确认后费用只能通过调整单修改");
        shipment.Currency = RmbMoneyService.Currency;
        return shipment;
    }

    private static void ApplyEditableValues(ShipmentExpense target, ShipmentExpense source)
    {
        target.ExpenseName = source.ExpenseName;
        target.NormalizedExpenseName = source.NormalizedExpenseName;
        target.ServiceType = source.ServiceType;
        target.LogisticsProviderId = source.LogisticsProviderId;
        target.SupplierId = null;
        target.ProviderCost = source.ProviderCost;
        target.CustomerCharge = source.CustomerCharge;
        target.ProfitAmount = source.ProfitAmount;
        target.Amount = source.ProviderCost;
        target.Currency = RmbMoneyService.Currency;
        target.NeedsCustomerChargeReview = false;
        target.Remark = source.Remark;
    }
}
