using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Futurem.Sourcing.Api.Services;

public sealed record SummaryAppendItem(long PurchaseOrderLineId, decimal Cartons);

public sealed class SummaryReservationService
{
    private static readonly string[] ActiveStatuses = new[] { "draft_reserved", "confirmed" };
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
        var transaction = await BeginTransactionAsync();
        try
        {
            var summary = await GetSummaryAsync(summaryOrderId);
            if (summary.Status != "draft")
                throw new BusinessRuleException("SUMMARY_STATUS_INVALID", "只有草稿汇总单可以预留商品");

            var item = await CreateReservationAsync(
                summary,
                purchaseOrderLineId,
                cartons,
                "draft_reserved",
                null,
                userId);
            await RecalculateSummaryAsync(summary.Id);
            await _db.SaveChangesAsync();
            if (transaction != null) await transaction.CommitAsync();
            return item;
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    public async Task<SummaryOrderItem> ReleaseAsync(
        long summaryOrderItemId,
        string reason,
        long? userId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("SUMMARY_RELEASE_REASON_REQUIRED", "释放预留必须填写原因");

        var transaction = await BeginTransactionAsync();
        try
        {
            var item = await _db.SummaryOrderItems.FirstOrDefaultAsync(x => x.Id == summaryOrderItemId)
                ?? throw new KeyNotFoundException("汇总商品不存在");
            var summary = await GetSummaryAsync(item.SummaryOrderId);
            if (summary.Status != "draft" || item.ReservationStatus != "draft_reserved")
                throw new BusinessRuleException("SUMMARY_ALLOCATION_LOCKED", "已确认的汇总商品不能释放");

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
            await RecalculateSummaryAsync(summary.Id);
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(
                nameof(SummaryOrderItem),
                item.Id,
                "release",
                before,
                new { item.ReservationStatus, item.ReleasedAt, item.ReleaseReason },
                reason,
                userId);
            if (transaction != null) await transaction.CommitAsync();
            return item;
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    public async Task<SummaryOrder> ConfirmAsync(long summaryOrderId, long? userId)
    {
        var transaction = await BeginTransactionAsync();
        try
        {
            var summary = await GetSummaryAsync(summaryOrderId);
            if (summary.Status == "confirmed") return summary;
            if (summary.Status != "draft")
                throw new BusinessRuleException("SUMMARY_NOT_DRAFT", "只有草稿汇总单可以确认");

            var items = await _db.SummaryOrderItems
                .Where(x => x.SummaryOrderId == summary.Id && x.ReservationStatus == "draft_reserved")
                .ToListAsync();
            if (items.Count == 0)
                throw new BusinessRuleException("SUMMARY_ITEMS_REQUIRED", "汇总单至少需要一个商品");

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
            if (transaction != null) await transaction.CommitAsync();
            return summary;
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    public async Task<SummaryOrder> AppendAsync(
        long summaryOrderId,
        IReadOnlyCollection<SummaryAppendItem> items,
        string reason,
        long? userId)
    {
        if (items.Count == 0)
            throw new BusinessRuleException("SUMMARY_ITEMS_REQUIRED", "请选择追加商品");
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("SUMMARY_APPEND_REASON_REQUIRED", "确认后追加商品必须填写原因");

        foreach (var input in items) ValidateWholeCartons(input.Cartons);

        var transaction = await BeginTransactionAsync();
        try
        {
            var summary = await GetSummaryAsync(summaryOrderId);
            if (summary.Status != "confirmed")
                throw new BusinessRuleException("SUMMARY_STATUS_INVALID", "只有已确认汇总单可以审批追加商品");

            var addedIds = new List<long>();
            foreach (var input in items)
            {
                var allocation = await CreateReservationAsync(
                    summary,
                    input.PurchaseOrderLineId,
                    input.Cartons,
                    "confirmed",
                    DateTime.Now,
                    userId);
                addedIds.Add(allocation.Id);
            }

            await RecalculateSummaryAsync(summary.Id);
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(
                nameof(SummaryOrder),
                summary.Id,
                "append_items",
                null,
                new { SummaryOrderItemIds = addedIds },
                reason,
                userId);
            if (transaction != null) await transaction.CommitAsync();
            return summary;
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    public async Task RecalculateSummaryAsync(long summaryOrderId)
    {
        var summary = await GetSummaryAsync(summaryOrderId);
        var items = await _db.SummaryOrderItems
            .Where(x => x.SummaryOrderId == summary.Id && ActiveStatuses.Contains(x.ReservationStatus))
            .ToListAsync();

        if (items.Count == 0)
        {
            SetTotals(summary, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
            return;
        }

        var lineIds = items.Select(x => x.PurchaseOrderLineId).Distinct().ToList();
        var lineList = await _db.DocumentLines
            .Where(x => !x.IsDeleted && lineIds.Contains(x.Id))
            .ToListAsync();
        var lines = lineList.ToDictionary(x => x.Id);

        decimal quantity = 0m;
        decimal cartons = 0m;
        decimal cbm = 0m;
        decimal grossWeight = 0m;
        decimal netWeight = 0m;
        decimal purchaseAmount = 0m;
        decimal salesAmount = 0m;

        foreach (var item in items)
        {
            DocumentLine? line;
            if (!lines.TryGetValue(item.PurchaseOrderLineId, out line) || line == null) continue;
            var cartonCbm = GetPerCarton(line.CartonCbm, line.TotalCbm, line.Cartons);
            var cartonGross = GetPerCarton(line.CartonGwKg, line.TotalGwKg, line.Cartons);
            var cartonNet = GetPerCarton(line.CartonNwKg, line.TotalNwKg, line.Cartons);
            quantity += item.ReservedQuantity;
            cartons += item.ReservedCartons;
            cbm += cartonCbm * item.ReservedCartons;
            grossWeight += cartonGross * item.ReservedCartons;
            netWeight += cartonNet * item.ReservedCartons;
            purchaseAmount += item.ReservedQuantity * line.PurchaseUnitPriceSnapshot;
            salesAmount += item.ReservedQuantity * line.SalesUnitPriceSnapshot;
        }

        SetTotals(summary, quantity, cartons, cbm, grossWeight, netWeight, purchaseAmount, salesAmount);
    }

    private async Task<SummaryOrderItem> CreateReservationAsync(
        SummaryOrder summary,
        long purchaseOrderLineId,
        decimal cartons,
        string reservationStatus,
        DateTime? confirmedAt,
        long? userId)
    {
        if (_db.Database.IsRelational())
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT `id` FROM `document_lines` WHERE `id` = {purchaseOrderLineId} FOR UPDATE");
        }

        var poLine = await _db.DocumentLines
            .FirstOrDefaultAsync(x => x.Id == purchaseOrderLineId && !x.IsDeleted)
            ?? throw new BusinessRuleException("PURCHASE_ORDER_LINE_NOT_FOUND", "采购订单商品不存在");
        if (poLine.DocumentType != "PO")
            throw new BusinessRuleException("PURCHASE_ORDER_LINE_INVALID", "所选明细不是采购订单商品");
        if (!poLine.OrderProductId.HasValue)
            throw new BusinessRuleException("ORDER_PRODUCT_LINK_REQUIRED", "采购订单商品缺少订单商品来源");

        var po = await _db.PurchaseOrders.FirstOrDefaultAsync(x => x.Id == poLine.DocumentId)
            ?? throw new BusinessRuleException("PURCHASE_ORDER_NOT_FOUND", "采购订单不存在");
        if (po.Status != "confirmed")
            throw new BusinessRuleException("PURCHASE_ORDER_NOT_CONFIRMED", "只有已确认采购订单可以汇总");
        if (!po.CustomerId.HasValue || po.CustomerId.Value != summary.CustomerId)
            throw new BusinessRuleException("SUMMARY_CUSTOMER_MISMATCH", "采购订单客户与汇总单客户不一致");
        if (poLine.Cartons <= 0m || poLine.CartonQty <= 0m)
            throw new BusinessRuleException("PURCHASE_ORDER_PACKING_REQUIRED", "采购订单商品缺少箱规");

        var activeReserved = await _db.SummaryOrderItems
            .Where(x => x.PurchaseOrderLineId == poLine.Id && ActiveStatuses.Contains(x.ReservationStatus))
            .SumAsync(x => (decimal?)x.ReservedCartons) ?? 0m;
        if (activeReserved + cartons > poLine.Cartons)
        {
            throw new BusinessRuleException(
                "SUMMARY_RESERVATION_CONFLICT",
                "采购订单商品剩余可汇总箱数不足",
                new
                {
                    PurchaseOrderLineId = poLine.Id,
                    TotalCartons = poLine.Cartons,
                    ReservedCartons = activeReserved,
                    RequestedCartons = cartons,
                    AvailableCartons = Math.Max(0m, poLine.Cartons - activeReserved)
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

    private async Task<SummaryOrder> GetSummaryAsync(long summaryOrderId)
    {
        return await _db.SummaryOrders.FirstOrDefaultAsync(x => x.Id == summaryOrderId)
            ?? throw new KeyNotFoundException("客户汇总单不存在");
    }

    private static void ValidateWholeCartons(decimal cartons)
    {
        if (cartons <= 0m)
            throw new BusinessRuleException("SUMMARY_CARTONS_REQUIRED", "汇总箱数必须大于零");
        if (cartons != decimal.Truncate(cartons))
            throw new BusinessRuleException("SUMMARY_WHOLE_CARTONS_REQUIRED", "客户汇总必须按整箱分配");
    }

    private static decimal GetPerCarton(decimal direct, decimal total, decimal cartons)
    {
        if (direct > 0m) return direct;
        return cartons > 0m ? total / cartons : 0m;
    }

    private static void SetTotals(
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

    private async Task<IDbContextTransaction?> BeginTransactionAsync()
    {
        if (!_db.Database.IsRelational()) return null;
        return await _db.Database.BeginTransactionAsync();
    }
}
