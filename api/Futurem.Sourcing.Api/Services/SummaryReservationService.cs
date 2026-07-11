using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Futurem.Sourcing.Api.Services;

public sealed record SummaryAppendItem(long PurchaseOrderLineId, decimal Cartons);

public sealed class SummaryReservationService
{
    private static readonly string[] ActiveReservationStatuses = ["draft_reserved", "confirmed"];

    private readonly AppDbContext _db;
    private readonly AuditTrailService _audit;

    public SummaryReservationService(AppDbContext db, AuditTrailService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<SummaryOrderItem> ReserveAsync(
        long summaryOrderId,
        long purchaseOrderLineId,
        decimal cartons,
        long? userId)
    {
        ValidateWholeCartons(cartons);
        await using var transaction = await BeginTransactionIfSupportedAsync();
        try
        {
            var summary = await RequireSummaryAsync(summaryOrderId, "draft");
            var allocation = await ReserveCoreAsync(
                summary,
                purchaseOrderLineId,
                cartons,
                "draft_reserved",
                null,
                userId);
            await RecalculateSummaryAsync(summary.Id);
            await _db.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();
            return allocation;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<SummaryOrderItem> ReleaseAsync(
        long summaryOrderItemId,
        string reason,
        long? userId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("SUMMARY_RELEASE_REASON_REQUIRED", "释放汇总预留必须填写原因");

        await using var transaction = await BeginTransactionIfSupportedAsync();
        try
        {
            var item = await _db.SummaryOrderItems.FirstOrDefaultAsync(x => x.Id == summaryOrderItemId)
                ?? throw new KeyNotFoundException("汇总商品预留不存在");
            var summary = await _db.SummaryOrders.FirstOrDefaultAsync(x => x.Id == item.SummaryOrderId)
                ?? throw new KeyNotFoundException("客户汇总单不存在");

            if (item.ReservationStatus != "draft_reserved" || summary.Status != "draft")
                throw new BusinessRuleException("SUMMARY_ALLOCATION_LOCKED", "已确认的汇总商品不能直接释放");

            var before = new
            {
                item.ReservationStatus,
                item.ReservedCartons,
                item.ReservedQuantity
            };
            item.ReservationStatus = "released";
            item.ReleasedAt = DateTime.Now;
            item.ReleaseReason = reason.Trim();
            item.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            await RecalculateSummaryAsync(summary.Id, item.Id);
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(
                nameof(SummaryOrderItem),
                item.Id,
                "release",
                before,
                new { item.ReservationStatus, item.ReleasedAt, item.ReleaseReason },
                reason,
                userId);
            if (transaction is not null) await transaction.CommitAsync();
            return item;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<SummaryOrder> ConfirmAsync(long summaryOrderId, long? userId)
    {
        await using var transaction = await BeginTransactionIfSupportedAsync();
        try
        {
            var summary = await _db.SummaryOrders.FirstOrDefaultAsync(x => x.Id == summaryOrderId)
                ?? throw new KeyNotFoundException("客户汇总单不存在");
            if (summary.Status == "confirmed") return summary;
            if (summary.Status != "draft")
                throw new BusinessRuleException("SUMMARY_NOT_DRAFT", "只有草稿汇总单可以确认");

            var items = await _db.SummaryOrderItems
                .Where(x => x.SummaryOrderId == summary.Id && x.ReservationStatus == "draft_reserved")
                .ToListAsync();
            if (items.Count == 0)
                throw new BusinessRuleException("SUMMARY_ITEMS_REQUIRED", "客户汇总单至少需要一个商品");

            var now = DateTime.Now;
            foreach (var item in items)
            {
                item.ReservationStatus = "confirmed";
                item.ConfirmedAt = now;
                item.UpdatedAt = now;
            }

            var before = new { summary.Status, summary.ConfirmedAt };
            summary.Status = "confirmed";
            summary.ConfirmedAt = now;
            summary.UpdatedAt = now;
            await RecalculateSummaryAsync(summary.Id);
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(
                nameof(SummaryOrder),
                summary.Id,
                "confirm",
                before,
                new { summary.Status, summary.ConfirmedAt },
                "确认客户汇总单",
                userId);
            if (transaction is not null) await transaction.CommitAsync();
            return summary;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<SummaryOrder> AppendAsync(
        long summaryOrderId,
        IReadOnlyCollection<SummaryAppendItem> items,
        string reason,
        long? userId)
    {
        if (items.Count == 0)
            throw new BusinessRuleException("SUMMARY_ITEMS_REQUIRED", "请选择要追加的商品");
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("SUMMARY_APPEND_REASON_REQUIRED", "确认后追加商品必须填写原因");
        foreach (var item in items) ValidateWholeCartons(item.Cartons);

        await using var transaction = await BeginTransactionIfSupportedAsync();
        try
        {
            var summary = await RequireSummaryAsync(summaryOrderId, "confirmed");
            var addedIds = new List<long>();
            foreach (var input in items)
            {
                var allocation = await ReserveCoreAsync(
                    summary,
                    input.PurchaseOrderLineId,
                    input.Cartons,
                    "confirmed",
                    DateTime.Now,
                    userId);
                addedIds.Add(allocation.Id);
            }

            await RecalculateSummaryAsync(summary.Id);
            summary.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(
                nameof(SummaryOrder),
                summary.Id,
                "append_items",
                null,
                new { addedSummaryOrderItemIds = addedIds },
                reason,
                userId);
            if (transaction is not null) await transaction.CommitAsync();
            return summary;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task RecalculateSummaryAsync(long summaryOrderId, long? excludedItemId = null)
    {
        var summary = await _db.SummaryOrders.FirstOrDefaultAsync(x => x.Id == summaryOrderId)
            ?? throw new KeyNotFoundException("客户汇总单不存在");
        var summaryItems = await _db.SummaryOrderItems
            .Where(x => x.SummaryOrderId == summary.Id)
            .ToListAsync();
        var items = summaryItems
            .Where(x => (!excludedItemId.HasValue || x.Id != excludedItemId.Value) &&
                        ActiveReservationStatuses.Contains(x.ReservationStatus))
            .ToList();

        if (items.Count == 0)
        {
            ApplyTotals(summary, 0, 0, 0, 0, 0, 0, 0);
            _db.Entry(summary).State = EntityState.Modified;
            return;
        }

        var lineIds = items.Select(x => x.PurchaseOrderLineId).Distinct().ToList();
        var lines = await _db.DocumentLines
            .Where(x => !x.IsDeleted && lineIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        decimal quantity = 0;
        decimal cartons = 0;
        decimal cbm = 0;
        decimal grossWeight = 0;
        decimal netWeight = 0;
        decimal purchaseAmount = 0;
        decimal salesAmount = 0;

        foreach (var item in items)
        {
            if (!lines.TryGetValue(item.PurchaseOrderLineId, out var line)) continue;
            var cartonCbm = PerCarton(line.CartonCbm, line.TotalCbm, line.Cartons);
            var cartonGross = PerCarton(line.CartonGwKg, line.TotalGwKg, line.Cartons);
            var cartonNet = PerCarton(line.CartonNwKg, line.TotalNwKg, line.Cartons);
            quantity += item.ReservedQuantity;
            cartons += item.ReservedCartons;
            cbm += cartonCbm * item.ReservedCartons;
            grossWeight += cartonGross * item.ReservedCartons;
            netWeight += cartonNet * item.ReservedCartons;
            purchaseAmount += item.ReservedQuantity * line.PurchaseUnitPriceSnapshot;
            salesAmount += item.ReservedQuantity * line.SalesUnitPriceSnapshot;
        }

        ApplyTotals(
            summary,
            quantity,
            cartons,
            cbm,
            grossWeight,
            netWeight,
            purchaseAmount,
            salesAmount);
        _db.Entry(summary).State = EntityState.Modified;
    }

    private async Task<SummaryOrderItem> ReserveCoreAsync(
        SummaryOrder summary,
        long purchaseOrderLineId,
        decimal cartons,
        string reservationStatus,
        DateTime? confirmedAt,
        long? userId)
    {
        var poLine = await LoadLockedPoLineAsync(purchaseOrderLineId)
            ?? throw new BusinessRuleException("PURCHASE_ORDER_LINE_NOT_FOUND", "采购订单商品明细不存在");
        if (poLine.DocumentType != "PO")
            throw new BusinessRuleException("PURCHASE_ORDER_LINE_INVALID", "所选明细不是采购订单商品");
        if (!poLine.OrderProductId.HasValue || poLine.OrderProductId.Value <= 0)
            throw new BusinessRuleException("ORDER_PRODUCT_LINK_REQUIRED", "采购订单明细缺少订单商品来源");

        var po = await _db.PurchaseOrders.FirstOrDefaultAsync(x => x.Id == poLine.DocumentId)
            ?? throw new BusinessRuleException("PURCHASE_ORDER_NOT_FOUND", "采购订单不存在");
        if (po.Status != "confirmed")
            throw new BusinessRuleException("PURCHASE_ORDER_NOT_CONFIRMED", "只有已确认采购订单可以加入客户汇总单");
        if (!po.CustomerId.HasValue || po.CustomerId.Value != summary.CustomerId)
            throw new BusinessRuleException("SUMMARY_CUSTOMER_MISMATCH", "采购订单客户与汇总单客户不一致");
        if (poLine.Cartons <= 0 || poLine.CartonQty <= 0)
            throw new BusinessRuleException("PURCHASE_ORDER_PACKING_REQUIRED", "采购订单明细缺少有效箱规");

        var activeReserved = await _db.SummaryOrderItems
            .Where(x => x.PurchaseOrderLineId == poLine.Id &&
                        ActiveReservationStatuses.Contains(x.ReservationStatus))
            .SumAsync(x => (decimal?)x.ReservedCartons) ?? 0m;
        if (activeReserved + cartons > poLine.Cartons)
        {
            throw new BusinessRuleException(
                "SUMMARY_RESERVATION_CONFLICT",
                "采购订单明细剩余可汇总箱数不足",
                new
                {
                    purchaseOrderLineId = poLine.Id,
                    totalCartons = poLine.Cartons,
                    reservedCartons = activeReserved,
                    requestedCartons = cartons,
                    availableCartons = Math.Max(0m, poLine.Cartons - activeReserved)
                });
        }

        var allocation = new SummaryOrderItem
        {
            SummaryOrderId = summary.Id,
            PurchaseOrderId = po.Id,
            PurchaseOrderLineId = poLine.Id,
            OrderProductId = poLine.OrderProductId.Value,
            SupplierId = po.SupplierId,
            ReservedCartons = cartons,
            ReservedQuantity = RmbMoneyService.Round(cartons * poLine.CartonQty),
            ReservationStatus = reservationStatus,
            ConfirmedAt = confirmedAt,
            CreatedBy = userId,
            CreatedAt = DateTime.Now
        };
        _db.SummaryOrderItems.Add(allocation);
        await _db.SaveChangesAsync();
        return allocation;
    }

    private async Task<DocumentLine?> LoadLockedPoLineAsync(long purchaseOrderLineId)
    {
        if (!_db.Database.IsRelational())
            return await _db.DocumentLines.FirstOrDefaultAsync(x => x.Id == purchaseOrderLineId && !x.IsDeleted);

        return await _db.DocumentLines
            .FromSqlInterpolated($"SELECT * FROM `document_lines` WHERE `id` = {purchaseOrderLineId} AND `is_deleted` = 0 FOR UPDATE")
            .SingleOrDefaultAsync();
    }

    private async Task<SummaryOrder> RequireSummaryAsync(long summaryOrderId, string expectedStatus)
    {
        var summary = await _db.SummaryOrders.FirstOrDefaultAsync(x => x.Id == summaryOrderId)
            ?? throw new KeyNotFoundException("客户汇总单不存在");
        if (summary.Status != expectedStatus)
        {
            throw new BusinessRuleException(
                "SUMMARY_STATUS_INVALID",
                $"客户汇总单状态必须为 {expectedStatus}");
        }
        return summary;
    }

    private static void ValidateWholeCartons(decimal cartons)
    {
        if (cartons <= 0)
            throw new BusinessRuleException("SUMMARY_CARTONS_REQUIRED", "汇总箱数必须大于零");
        if (cartons != decimal.Truncate(cartons))
            throw new BusinessRuleException("SUMMARY_WHOLE_CARTONS_REQUIRED", "客户汇总必须按整箱数量分配");
    }

    private static decimal PerCarton(decimal direct, decimal total, decimal cartons)
        => direct > 0 ? direct : cartons > 0 ? total / cartons : 0m;

    private static void ApplyTotals(
        SummaryOrder summary,
        decimal quantity,
        decimal cartons,
        decimal cbm,
        decimal grossWeight,
        decimal netWeight,
        decimal purchaseAmount,
        decimal salesAmount)
    {
        summary.TotalQuantity = RmbMoneyService.Round(quantity);
        summary.TotalCartons = RmbMoneyService.Round(cartons);
        summary.TotalCbm = RmbMoneyService.Round(cbm);
        summary.TotalGrossWeightKg = RmbMoneyService.Round(grossWeight);
        summary.TotalNetWeightKg = RmbMoneyService.Round(netWeight);
        summary.PurchaseAmount = RmbMoneyService.Round(purchaseAmount);
        summary.SalesAmount = RmbMoneyService.Round(salesAmount);
        summary.ExpectedProfit = RmbMoneyService.Round(summary.SalesAmount - summary.PurchaseAmount);
        summary.GoodsAmount = summary.SalesAmount;
        summary.ReceivableAmount = 0m;
        summary.Currency = RmbMoneyService.Currency;
        summary.UpdatedAt = DateTime.Now;
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync()
        => _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;
}
