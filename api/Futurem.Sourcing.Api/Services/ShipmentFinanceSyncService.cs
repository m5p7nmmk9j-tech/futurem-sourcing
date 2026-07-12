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
        var shipment = await _db.Shipments.FindAsync(shipmentId)
            ?? throw new KeyNotFoundException("Shipment not found");
        if (!SyncableStatuses.Contains(shipment.Status))
            throw new BusinessRuleException("SHIPMENT_NOT_READY_FOR_FINANCE", "草稿出运单不能同步财务");

        await _expenseService.ValidateAllAsync(shipmentId);
        var expenses = await _db.ShipmentExpenses
            .Where(x => x.ShipmentId == shipmentId)
            .OrderBy(x => x.SortNo)
            .ThenBy(x => x.Id)
            .ToListAsync();

        foreach (var expense in expenses)
            await SyncExpenseAsync(shipment, expense);

        shipment.ExpenseTotal = RmbMoneyService.Round(expenses.Sum(x => x.ProviderCost));
        shipment.CustomerChargeTotal = RmbMoneyService.Round(expenses.Sum(x => x.CustomerCharge));
        shipment.LogisticsProfitTotal = RmbMoneyService.Round(expenses.Sum(x => x.ProfitAmount));
        shipment.Currency = RmbMoneyService.Currency;
        shipment.FinanceSyncStatus = "synced";
        shipment.FinanceSyncMessage = null;
        shipment.FinanceSyncedAt = DateTime.Now;
        shipment.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    private async Task SyncExpenseAsync(Shipment shipment, ShipmentExpense expense)
    {
        var finance = await FindCurrentFinanceAsync(expense);

        if (finance is not null && finance.LogisticsProviderId != expense.LogisticsProviderId)
        {
            await CloseForProviderChangeAsync(finance);
            expense.FinanceRecordId = null;
            finance = null;
        }

        if (finance is null)
        {
            if (expense.ProviderCost <= 0m)
            {
                expense.FinanceStatus = "not_generated";
                expense.UpdatedAt = DateTime.Now;
                return;
            }
            if (!expense.LogisticsProviderId.HasValue)
                throw new BusinessRuleException("LOGISTICS_PROVIDER_REQUIRED", "物流费用缺少物流服务商");

            finance = new FinanceRecord
            {
                No = NumberService.NewNo("AP"),
                RecordType = "payable",
                TargetType = "SHIPMENT_EXPENSE",
                TargetId = expense.Id,
                ShipmentExpenseId = expense.Id,
                SourceKey = $"shipment:{shipment.Id}:expense:{expense.Id}:provider",
                SupplierId = null,
                LogisticsProviderId = expense.LogisticsProviderId,
                CounterpartyType = "logistics_provider",
                Currency = RmbMoneyService.Currency,
                Amount = expense.ProviderCost,
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

        finance.SupplierId = null;
        finance.LogisticsProviderId = expense.LogisticsProviderId;
        finance.CounterpartyType = "logistics_provider";
        finance.Currency = RmbMoneyService.Currency;
        finance.TargetId = expense.Id;
        finance.ShipmentExpenseId = expense.Id;
        finance.SourceKey = $"shipment:{shipment.Id}:expense:{expense.Id}:provider";
        finance.Remark = $"出运单 {shipment.No} / {expense.ExpenseName}";
        finance.RecordDate ??= shipment.Etd ?? DateTime.Today;
        finance.Amount = RmbMoneyService.Round(expense.ProviderCost);

        // Old product-supplier prepayment applications are released during migration.
        if (finance.PrepaymentAppliedAmount > 0m)
            await _prepaymentService.ReleaseApplicationsAsync(finance, 0m);
        finance.OverpaymentTransferredAmount = 0m;
        FinanceBalanceService.RefreshStatus(finance);
        finance.UpdatedAt = DateTime.Now;
        expense.FinanceRecordId = finance.Id;
        expense.FinanceStatus = finance.Status;
        expense.Amount = expense.ProviderCost;
        expense.Currency = RmbMoneyService.Currency;
        expense.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    private async Task<FinanceRecord?> FindCurrentFinanceAsync(ShipmentExpense expense)
    {
        if (expense.FinanceRecordId.HasValue)
        {
            var byId = await _db.FinanceRecords.FindAsync(expense.FinanceRecordId.Value);
            if (byId is not null) return byId;
        }

        return await _db.FinanceRecords.FirstOrDefaultAsync(x =>
            x.RecordType == "payable" && x.ShipmentExpenseId == expense.Id);
    }

    private async Task CloseForProviderChangeAsync(FinanceRecord finance)
    {
        if (finance.PrepaymentAppliedAmount > 0m)
            await _prepaymentService.ReleaseApplicationsAsync(finance, 0m);
        finance.Amount = 0m;
        finance.ShipmentExpenseId = null;
        finance.SourceKey = $"{finance.SourceKey}:HISTORY:{finance.Id}";
        FinanceBalanceService.RefreshStatus(finance);
        finance.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
    }
}
