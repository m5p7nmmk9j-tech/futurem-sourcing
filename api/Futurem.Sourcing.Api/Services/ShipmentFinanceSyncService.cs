using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public class ShipmentFinanceSyncService
{
    private static readonly string[] SyncableStatuses = ["confirmed", "shipped", "completed"];

    private readonly AppDbContext _db;
    private readonly ShipmentExpenseService _expenseService;
    private readonly SupplierPrepaymentService _prepaymentService;

    public ShipmentFinanceSyncService(
        AppDbContext db,
        ShipmentExpenseService expenseService,
        SupplierPrepaymentService prepaymentService)
    {
        _db = db;
        _expenseService = expenseService;
        _prepaymentService = prepaymentService;
    }

    public async Task SyncAsync(long shipmentId)
    {
        var shipment = await _db.Shipments.FindAsync(shipmentId);
        if (shipment == null) throw new KeyNotFoundException("Shipment not found");
        if (!SyncableStatuses.Contains(shipment.Status))
            throw new InvalidOperationException("草稿出运单不能同步财务");

        await _expenseService.ValidateAllAsync(shipmentId);
        var expenses = await _db.ShipmentExpenses
            .Where(x => x.ShipmentId == shipmentId)
            .OrderBy(x => x.SortNo)
            .ThenBy(x => x.Id)
            .ToListAsync();

        foreach (var expense in expenses)
            await SyncExpenseAsync(shipment, expense);

        shipment.ExpenseTotal = FinanceBalanceService.Round2(expenses.Sum(x => x.Amount));
        shipment.FinanceSyncStatus = "synced";
        shipment.FinanceSyncMessage = null;
        shipment.FinanceSyncedAt = DateTime.Now;
        shipment.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    private async Task SyncExpenseAsync(Shipment shipment, ShipmentExpense expense)
    {
        var finance = await FindCurrentFinanceAsync(expense);

        if (finance != null && finance.SupplierId != expense.SupplierId)
        {
            await CloseForSupplierChangeAsync(finance);
            expense.FinanceRecordId = null;
            finance = null;
        }

        if (finance == null)
        {
            if (expense.Amount <= 0m)
            {
                expense.FinanceStatus = "not_generated";
                expense.UpdatedAt = DateTime.Now;
                return;
            }

            finance = new FinanceRecord
            {
                No = NumberService.NewNo("AP"),
                RecordType = "payable",
                TargetType = "SHIPMENT_EXPENSE",
                TargetId = expense.Id,
                ShipmentExpenseId = expense.Id,
                SourceKey = $"SHIPMENT_EXPENSE:{expense.Id}",
                SupplierId = expense.SupplierId,
                Currency = shipment.Currency,
                Amount = expense.Amount,
                PaidAmount = 0m,
                PrepaymentAppliedAmount = 0m,
                OverpaymentTransferredAmount = 0m,
                RecordDate = shipment.Etd ?? DateTime.Today,
                Status = "pending",
                Remark = $"出运单 {shipment.No} / {expense.ExpenseName}",
                CreatedAt = DateTime.Now
            };
            FinanceBalanceService.RefreshStatus(finance);
            _db.FinanceRecords.Add(finance);
            await _db.SaveChangesAsync();
            expense.FinanceRecordId = finance.Id;
        }

        finance.SupplierId = expense.SupplierId;
        finance.Currency = shipment.Currency;
        finance.TargetId = expense.Id;
        finance.ShipmentExpenseId = expense.Id;
        finance.SourceKey = $"SHIPMENT_EXPENSE:{expense.Id}";
        finance.Remark = $"出运单 {shipment.No} / {expense.ExpenseName}";
        finance.RecordDate ??= shipment.Etd ?? DateTime.Today;
        finance.Amount = FinanceBalanceService.Round2(expense.Amount);

        var creditToKeep = Math.Max(0m, FinanceBalanceService.Round2(finance.Amount - finance.PaidAmount));
        await _prepaymentService.ReleaseApplicationsAsync(finance, creditToKeep);

        var desiredCashTransfer = Math.Max(0m, FinanceBalanceService.Round2(finance.PaidAmount - finance.Amount));
        await _prepaymentService.UpsertOverpaymentAsync(finance, desiredCashTransfer);

        FinanceBalanceService.RefreshStatus(finance);
        if (FinanceBalanceService.Outstanding(finance) > 0m)
            await _prepaymentService.ApplyAvailableAsync(finance);

        FinanceBalanceService.RefreshStatus(finance);
        finance.UpdatedAt = DateTime.Now;
        expense.FinanceRecordId = finance.Id;
        expense.FinanceStatus = finance.Status;
        expense.Currency = shipment.Currency;
        expense.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    private async Task<FinanceRecord?> FindCurrentFinanceAsync(ShipmentExpense expense)
    {
        if (expense.FinanceRecordId.HasValue)
        {
            var byId = await _db.FinanceRecords.FindAsync(expense.FinanceRecordId.Value);
            if (byId != null) return byId;
        }

        return await _db.FinanceRecords.FirstOrDefaultAsync(x =>
            x.RecordType == "payable" && x.ShipmentExpenseId == expense.Id);
    }

    private async Task CloseForSupplierChangeAsync(FinanceRecord finance)
    {
        await _prepaymentService.ReleaseApplicationsAsync(finance, 0m);
        finance.Amount = 0m;
        await _prepaymentService.UpsertOverpaymentAsync(finance, finance.PaidAmount);
        finance.ShipmentExpenseId = null;
        finance.SourceKey = $"{finance.SourceKey}:HISTORY:{finance.Id}";
        FinanceBalanceService.RefreshStatus(finance);
        finance.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
    }
}
