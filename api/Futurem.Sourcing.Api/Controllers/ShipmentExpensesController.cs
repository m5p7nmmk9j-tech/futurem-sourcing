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
    private static readonly string[] SyncableStatuses = ["confirmed", "shipped", "completed"];

    private readonly AppDbContext _db;
    private readonly ShipmentExpenseService _expenseService;
    private readonly ShipmentFinanceSyncService _financeSyncService;

    public ShipmentExpensesController(
        AppDbContext db,
        ShipmentExpenseService expenseService,
        ShipmentFinanceSyncService financeSyncService)
    {
        _db = db;
        _expenseService = expenseService;
        _financeSyncService = financeSyncService;
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
        var financeIds = expenses.Where(x => x.FinanceRecordId.HasValue).Select(x => x.FinanceRecordId!.Value).ToList();
        var finances = await _db.FinanceRecords.Where(x => financeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

        return expenses.Select(x =>
        {
            FinanceRecord? finance = null;
            if (x.FinanceRecordId.HasValue) finances.TryGetValue(x.FinanceRecordId.Value, out finance);
            return (object)new
            {
                x.Id,
                x.ShipmentId,
                x.ExpenseCode,
                x.ExpenseName,
                x.IsCustom,
                x.SupplierId,
                x.Amount,
                x.Currency,
                x.FinanceRecordId,
                financeNo = finance?.No,
                paidAmount = finance?.PaidAmount ?? 0m,
                prepaymentAppliedAmount = finance?.PrepaymentAppliedAmount ?? 0m,
                overpaymentTransferredAmount = finance?.OverpaymentTransferredAmount ?? 0m,
                outstandingAmount = finance == null ? x.Amount : FinanceBalanceService.Outstanding(finance),
                financeStatus = finance?.Status ?? x.FinanceStatus,
                x.Remark,
                x.SortNo
            };
        }).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<ShipmentExpense>> Create(long shipmentId, ShipmentExpense input)
    {
        var shipment = await _db.Shipments.FindAsync(shipmentId);
        if (shipment == null) return NotFound();
        if (!input.IsCustom) return BadRequest(new { message = "固定费用由系统自动创建" });

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
            if (deleted != null)
            {
                deleted.IsDeleted = false;
                deleted.ExpenseName = input.ExpenseName;
                deleted.NormalizedExpenseName = input.NormalizedExpenseName;
                deleted.SupplierId = input.SupplierId;
                deleted.Amount = input.Amount;
                deleted.Currency = shipment.Currency;
                deleted.Remark = input.Remark;
                deleted.UpdatedAt = DateTime.Now;
                input = deleted;
            }
            else
            {
                var nextSort = await _db.ShipmentExpenses.Where(x => x.ShipmentId == shipmentId).MaxAsync(x => (int?)x.SortNo) ?? 40;
                input.SortNo = nextSort + 10;
                _db.ShipmentExpenses.Add(input);
            }

            await _db.SaveChangesAsync();
            await _expenseService.RecalculateExpenseTotalAsync(shipmentId);
            if (SyncableStatuses.Contains(shipment.Status)) await _financeSyncService.SyncAsync(shipmentId);
            if (transaction != null) await transaction.CommitAsync();
            return input;
        }
        catch (Exception ex)
        {
            if (transaction != null) await transaction.RollbackAsync();
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{expenseId:long}")]
    public async Task<ActionResult<ShipmentExpense>> Update(long shipmentId, long expenseId, ShipmentExpense input)
    {
        var shipment = await _db.Shipments.FindAsync(shipmentId);
        if (shipment == null) return NotFound();
        var entity = await _db.ShipmentExpenses.FirstOrDefaultAsync(x => x.Id == expenseId && x.ShipmentId == shipmentId);
        if (entity == null) return NotFound();

        await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;
        try
        {
            entity.ExpenseName = entity.IsCustom ? input.ExpenseName : entity.ExpenseName;
            entity.SupplierId = input.SupplierId;
            entity.Amount = input.Amount;
            entity.Currency = shipment.Currency;
            entity.Remark = input.Remark;
            entity.UpdatedAt = DateTime.Now;
            await _expenseService.ValidateAsync(shipment, entity, entity.Id);
            await _db.SaveChangesAsync();
            await _expenseService.RecalculateExpenseTotalAsync(shipmentId);
            if (SyncableStatuses.Contains(shipment.Status)) await _financeSyncService.SyncAsync(shipmentId);
            if (transaction != null) await transaction.CommitAsync();
            return entity;
        }
        catch (Exception ex)
        {
            if (transaction != null) await transaction.RollbackAsync();
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{expenseId:long}")]
    public async Task<IActionResult> Delete(long shipmentId, long expenseId)
    {
        var shipment = await _db.Shipments.FindAsync(shipmentId);
        if (shipment == null) return NotFound();
        var entity = await _db.ShipmentExpenses.FirstOrDefaultAsync(x => x.Id == expenseId && x.ShipmentId == shipmentId);
        if (entity == null) return NotFound();

        var finance = entity.FinanceRecordId.HasValue ? await _db.FinanceRecords.FindAsync(entity.FinanceRecordId.Value) : null;
        if (entity.IsCustom && finance != null && (finance.PaidAmount > 0m || finance.PrepaymentAppliedAmount > 0m))
            return BadRequest(new { message = "已有付款或预付款抵扣，不能直接删除该费用，请将金额调整为 0" });

        await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;
        try
        {
            entity.Amount = 0m;
            entity.SupplierId = null;
            entity.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            if (SyncableStatuses.Contains(shipment.Status)) await _financeSyncService.SyncAsync(shipmentId);

            if (entity.IsCustom)
            {
                entity.IsDeleted = true;
                entity.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
            }
            await _expenseService.RecalculateExpenseTotalAsync(shipmentId);
            if (transaction != null) await transaction.CommitAsync();
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            if (transaction != null) await transaction.RollbackAsync();
            return BadRequest(new { message = ex.Message });
        }
    }
}
