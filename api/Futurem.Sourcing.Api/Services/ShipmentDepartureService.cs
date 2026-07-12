using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Futurem.Sourcing.Api.Services;

public sealed record ShipmentDepartureResult(
    Shipment Shipment,
    FinanceRecord CustomerReceivable,
    IReadOnlyCollection<FinanceRecord> ProviderPayables);

public sealed class ShipmentDepartureService
{
    private readonly AppDbContext _db;
    private readonly ShipmentExpenseService _expenseService;
    private readonly FinanceDocumentService _finance;
    private readonly AuditTrailService _audit;

    public ShipmentDepartureService(
        AppDbContext db,
        ShipmentExpenseService expenseService,
        FinanceDocumentService finance,
        AuditTrailService audit)
    {
        _db = db;
        _expenseService = expenseService;
        _finance = finance;
        _audit = audit;
    }

    public async Task<ShipmentDepartureResult> ConfirmDepartureAsync(
        long shipmentId,
        long? userId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginTransactionIfSupportedAsync(cancellationToken);
        try
        {
            var shipment = await LoadShipmentForUpdateAsync(shipmentId, cancellationToken)
                ?? throw new KeyNotFoundException("出运单不存在");

            if (shipment.Status == "draft")
                throw new BusinessRuleException("SHIPMENT_NOT_CONFIRMED", "请先确认出运资料，再确认货柜离仓发运");
            if (shipment.Status is not ("confirmed" or "shipped" or "completed"))
                throw new BusinessRuleException("SHIPMENT_STATUS_INVALID", "当前出运单状态不能确认发运");
            if (!shipment.ContainerLoadId.HasValue)
                throw new BusinessRuleException("SHIPMENT_CONTAINER_REQUIRED", "出运单必须关联装柜单");
            if (!shipment.CustomerId.HasValue)
                throw new BusinessRuleException("SHIPMENT_CUSTOMER_REQUIRED", "出运单缺少客户");
            if (string.IsNullOrWhiteSpace(shipment.ContainerNo))
                throw new BusinessRuleException("SHIPMENT_CONTAINER_NO_REQUIRED", "确认发运前必须填写柜号");

            var container = await _db.ContainerLoads
                .FirstOrDefaultAsync(x => x.Id == shipment.ContainerLoadId.Value, cancellationToken)
                ?? throw new BusinessRuleException("SHIPMENT_CONTAINER_NOT_FOUND", "关联装柜单不存在");
            if (container.Status is not ("shipment_created" or "completed" or "confirmed"))
                throw new BusinessRuleException("CONTAINER_NOT_READY_FOR_SHIPMENT", "关联装柜单尚未完成实际装柜确认");
            if (container.CustomerId != shipment.CustomerId)
                throw new BusinessRuleException("SHIPMENT_CUSTOMER_MISMATCH", "出运单客户与装柜单客户不一致");

            await _expenseService.ValidateAllAsync(shipment.Id);
            var expenses = await _db.ShipmentExpenses
                .Where(x => x.ShipmentId == shipment.Id)
                .OrderBy(x => x.SortNo)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

            var providerPayables = new List<FinanceRecord>();
            foreach (var expense in expenses.Where(x => x.ProviderCost > 0m))
            {
                if (!expense.LogisticsProviderId.HasValue)
                    throw new BusinessRuleException("LOGISTICS_PROVIDER_REQUIRED", $"{expense.ExpenseName}缺少物流服务商");

                var payableSourceKey = $"shipment:{shipment.Id}:expense:{expense.Id}:provider";
                var payable = await _finance.EnsureLogisticsProviderPayableAsync(
                    payableSourceKey,
                    "SHIPMENT_EXPENSE",
                    expense.Id,
                    expense.LogisticsProviderId.Value,
                    [new FinanceLineInput(
                        payableSourceKey,
                        "logistics_provider_cost",
                        1m,
                        expense.ProviderCost,
                        expense.ProviderCost,
                        $"出运单 {shipment.No} / {expense.ExpenseName} 服务商成本",
                        "SHIPMENT_EXPENSE",
                        expense.Id)],
                    cancellationToken);
                payable.ShipmentExpenseId = expense.Id;
                payable.RecordDate ??= shipment.ActualDepartureAt ?? DateTime.Now;
                expense.FinanceRecordId = payable.Id;
                expense.FinanceStatus = payable.Status;
                providerPayables.Add(payable);
            }

            var receivableSourceKey = $"container:{container.Id}:goods";
            var receivable = await _db.FinanceRecords
                .FirstOrDefaultAsync(x => x.SourceKey == receivableSourceKey, cancellationToken);
            if (receivable is null)
                receivable = await _finance.EnsureCustomerReceivableForContainerAsync(container.Id);

            var customerChargeLines = expenses
                .Where(x => x.CustomerCharge > 0m)
                .Select(expense => new FinanceLineInput(
                    $"shipment:{shipment.Id}:expense:{expense.Id}:customer",
                    "logistics_customer_charge",
                    1m,
                    expense.CustomerCharge,
                    expense.CustomerCharge,
                    $"出运单 {shipment.No} / {expense.ExpenseName} 客户收费",
                    "SHIPMENT_EXPENSE",
                    expense.Id))
                .ToList();

            receivable = await _finance.EnsureReceivableAsync(
                receivableSourceKey,
                "CONTAINER_LOAD",
                container.Id,
                shipment.CustomerId.Value,
                customerChargeLines,
                cancellationToken);

            var now = DateTime.Now;
            shipment.Status = "shipped";
            shipment.Currency = RmbMoneyService.Currency;
            shipment.ActualDepartureAt ??= now;
            shipment.ExpenseTotal = RmbMoneyService.Round(expenses.Sum(x => x.ProviderCost));
            shipment.CustomerChargeTotal = RmbMoneyService.Round(expenses.Sum(x => x.CustomerCharge));
            shipment.LogisticsProfitTotal = RmbMoneyService.Round(expenses.Sum(x => x.ProfitAmount));
            shipment.FinanceSyncStatus = "synced";
            shipment.FinanceSyncMessage = null;
            shipment.FinanceSyncedAt = now;
            shipment.UpdatedBy = userId;
            shipment.UpdatedAt = now;
            container.Status = "completed";
            container.UpdatedBy = userId;
            container.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);

            await _audit.WriteAsync(
                nameof(Shipment),
                shipment.Id,
                "confirm_departure",
                new { status = shipment.Status == "shipped" ? "confirmed" : shipment.Status },
                new
                {
                    shipment.Status,
                    shipment.ActualDepartureAt,
                    shipment.ExpenseTotal,
                    shipment.CustomerChargeTotal,
                    shipment.LogisticsProfitTotal,
                    customerReceivableId = receivable.Id,
                    providerPayableIds = providerPayables.Select(x => x.Id).ToArray()
                },
                "货柜实际离开仓库，确认发运",
                userId);

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            return new ShipmentDepartureResult(shipment, receivable, providerPayables);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<Shipment?> LoadShipmentForUpdateAsync(long shipmentId, CancellationToken cancellationToken)
    {
        if (!_db.Database.IsRelational())
            return await _db.Shipments.FirstOrDefaultAsync(x => x.Id == shipmentId, cancellationToken);

        return await _db.Shipments
            .FromSqlInterpolated($"SELECT * FROM `shipments` WHERE `id` = {shipmentId} AND `is_deleted` = 0 FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync(CancellationToken cancellationToken)
        => _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;
}
