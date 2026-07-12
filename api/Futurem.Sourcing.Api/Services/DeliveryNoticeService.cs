using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Futurem.Sourcing.Api.Services;

public sealed record ReceivingLineInput(
    long DeliveryNoticeLineId,
    decimal Quantity,
    decimal Cartons,
    string? Remark);

public sealed class DeliveryNoticeService
{
    private readonly AppDbContext _db;
    private readonly AuditTrailService _audit;

    public DeliveryNoticeService(AppDbContext db, AuditTrailService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<DeliveryNotice>> GenerateForConfirmedSummaryAsync(
        long summaryOrderId,
        DateTime plannedDate,
        long warehouseId,
        long? userId)
    {
        if (warehouseId <= 0)
            throw new BusinessRuleException("WAREHOUSE_REQUIRED", "请选择收货仓库");

        await using var transaction = await BeginTransactionIfSupportedAsync();
        try
        {
            var summary = await _db.SummaryOrders.FirstOrDefaultAsync(x => x.Id == summaryOrderId)
                ?? throw new KeyNotFoundException("客户汇总单不存在");
            if (summary.Status != "confirmed")
                throw new BusinessRuleException("SUMMARY_NOT_CONFIRMED", "只有已确认客户汇总单可以生成送货通知");

            var summaryItems = await _db.SummaryOrderItems
                .Where(x => x.SummaryOrderId == summary.Id && x.ReservationStatus == "confirmed")
                .OrderBy(x => x.SupplierId)
                .ThenBy(x => x.Id)
                .ToListAsync();
            if (summaryItems.Count == 0)
                throw new BusinessRuleException("SUMMARY_ITEMS_REQUIRED", "客户汇总单没有已确认商品");

            var results = new List<DeliveryNotice>();
            foreach (var group in summaryItems.GroupBy(x => x.SupplierId))
            {
                var date = plannedDate.Date;
                var sourceKey = $"summary:{summary.Id}:supplier:{group.Key}:warehouse:{warehouseId}:date:{date:yyyyMMdd}";
                var existing = await _db.DeliveryNotices.FirstOrDefaultAsync(x => x.SourceKey == sourceKey);
                if (existing is not null)
                {
                    results.Add(existing);
                    continue;
                }

                var groupItems = group.ToList();
                var summaryItemIds = groupItems.Select(x => x.Id).ToList();
                var alreadyPlanned = await _db.DeliveryNoticeLines
                    .Where(x => summaryItemIds.Contains(x.SummaryOrderItemId))
                    .GroupBy(x => x.SummaryOrderItemId)
                    .Select(x => new
                    {
                        SummaryOrderItemId = x.Key,
                        Cartons = x.Sum(y => y.PlannedCartons),
                        Quantity = x.Sum(y => y.PlannedQuantity)
                    })
                    .ToListAsync();
                var plannedByItem = alreadyPlanned.ToDictionary(x => x.SummaryOrderItemId);

                foreach (var item in groupItems)
                {
                    var used = plannedByItem.GetValueOrDefault(item.Id);
                    if ((used?.Cartons ?? 0m) + item.ReservedCartons > item.ReservedCartons ||
                        (used?.Quantity ?? 0m) + item.ReservedQuantity > item.ReservedQuantity)
                    {
                        throw new BusinessRuleException(
                            "DELIVERY_NOTICE_OVER_PLANNED",
                            "送货通知累计数量超过客户汇总单确认数量",
                            new
                            {
                                summaryOrderItemId = item.Id,
                                confirmedCartons = item.ReservedCartons,
                                confirmedQuantity = item.ReservedQuantity,
                                alreadyPlannedCartons = used?.Cartons ?? 0m,
                                alreadyPlannedQuantity = used?.Quantity ?? 0m
                            });
                    }
                }

                var notice = new DeliveryNotice
                {
                    No = NumberService.NewNo("DN"),
                    SourceKey = sourceKey,
                    SummaryOrderId = summary.Id,
                    SupplierId = group.Key,
                    WarehouseId = warehouseId,
                    PlannedDeliveryDate = date,
                    Status = "draft",
                    TotalCartons = RmbMoneyService.Round(groupItems.Sum(x => x.ReservedCartons)),
                    TotalQuantity = RmbMoneyService.Round(groupItems.Sum(x => x.ReservedQuantity)),
                    CreatedBy = userId,
                    CreatedAt = DateTime.Now
                };
                _db.DeliveryNotices.Add(notice);
                await _db.SaveChangesAsync();

                foreach (var item in groupItems)
                {
                    _db.DeliveryNoticeLines.Add(new DeliveryNoticeLine
                    {
                        DeliveryNoticeId = notice.Id,
                        SummaryOrderItemId = item.Id,
                        PurchaseOrderId = item.PurchaseOrderId,
                        PurchaseOrderLineId = item.PurchaseOrderLineId,
                        OrderProductId = item.OrderProductId,
                        PlannedCartons = item.ReservedCartons,
                        PlannedQuantity = item.ReservedQuantity,
                        CreatedBy = userId,
                        CreatedAt = DateTime.Now
                    });
                }
                await _db.SaveChangesAsync();
                await _audit.WriteAsync(
                    nameof(DeliveryNotice),
                    notice.Id,
                    "generate",
                    null,
                    new { notice.SourceKey, notice.TotalCartons, notice.TotalQuantity },
                    "客户汇总单确认后生成送货通知",
                    userId);
                results.Add(notice);
            }

            if (transaction is not null) await transaction.CommitAsync();
            return results;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<DeliveryNotice> PublishAsync(long deliveryNoticeId, long? userId)
    {
        var notice = await _db.DeliveryNotices.FirstOrDefaultAsync(x => x.Id == deliveryNoticeId)
            ?? throw new KeyNotFoundException("送货通知不存在");
        if (notice.Status is "published" or "supplier_confirmed" or "partially_received" or "received")
            return notice;
        if (notice.Status != "draft")
            throw new BusinessRuleException("DELIVERY_NOTICE_STATUS_INVALID", "当前送货通知状态不能发布");

        var before = new { notice.Status, notice.PublishedAt };
        notice.Status = "published";
        notice.PublishedAt = DateTime.Now;
        notice.UpdatedBy = userId;
        notice.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(
            nameof(DeliveryNotice),
            notice.Id,
            "publish",
            before,
            new { notice.Status, notice.PublishedAt },
            "发布供应商送货通知",
            userId);
        return notice;
    }

    public async Task<DeliveryNotice> SupplierConfirmAsync(long deliveryNoticeId, long? userId)
    {
        var notice = await _db.DeliveryNotices.FirstOrDefaultAsync(x => x.Id == deliveryNoticeId)
            ?? throw new KeyNotFoundException("送货通知不存在");
        if (notice.Status == "supplier_confirmed") return notice;
        if (notice.Status != "published")
            throw new BusinessRuleException("DELIVERY_NOTICE_NOT_PUBLISHED", "送货通知发布后才能由供应商确认");

        notice.Status = "supplier_confirmed";
        notice.SupplierConfirmedAt = DateTime.Now;
        notice.UpdatedBy = userId;
        notice.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(
            nameof(DeliveryNotice),
            notice.Id,
            "supplier_confirm",
            null,
            new { notice.Status, notice.SupplierConfirmedAt },
            "供应商确认送货通知",
            userId);
        return notice;
    }

    public async Task<ReceivingOrder> CreateReceivingAsync(
        long deliveryNoticeId,
        IReadOnlyCollection<ReceivingLineInput> lines,
        long? userId)
    {
        if (lines.Count == 0)
            throw new BusinessRuleException("RECEIVING_LINES_REQUIRED", "收货单至少需要一个商品");
        if (lines.Any(x => x.Quantity <= 0 || x.Cartons <= 0))
            throw new BusinessRuleException("RECEIVING_QUANTITY_INVALID", "收货数量和箱数必须大于零");
        if (lines.GroupBy(x => x.DeliveryNoticeLineId).Any(x => x.Count() > 1))
            throw new BusinessRuleException("RECEIVING_LINE_DUPLICATED", "同一送货通知商品不能重复填写");

        await using var transaction = await BeginTransactionIfSupportedAsync();
        try
        {
            var notice = await _db.DeliveryNotices.FirstOrDefaultAsync(x => x.Id == deliveryNoticeId)
                ?? throw new KeyNotFoundException("送货通知不存在");
            if (notice.Status is "cancelled" or "closed")
                throw new BusinessRuleException("DELIVERY_NOTICE_CLOSED", "送货通知已关闭，不能继续收货");

            var requestedIds = lines.Select(x => x.DeliveryNoticeLineId).ToList();
            var noticeLines = await _db.DeliveryNoticeLines
                .Where(x => x.DeliveryNoticeId == notice.Id && requestedIds.Contains(x.Id))
                .ToListAsync();
            if (noticeLines.Count != requestedIds.Count)
                throw new BusinessRuleException("DELIVERY_NOTICE_LINE_NOT_FOUND", "部分收货商品不属于当前送货通知");

            var inputById = lines.ToDictionary(x => x.DeliveryNoticeLineId);
            foreach (var line in noticeLines)
            {
                var input = inputById[line.Id];
                if (line.ReceivedQuantity + input.Quantity > line.PlannedQuantity ||
                    line.ReceivedCartons + input.Cartons > line.PlannedCartons)
                {
                    throw new BusinessRuleException(
                        "DELIVERY_NOTICE_OVER_PLANNED",
                        "累计到货数量超过送货通知计划数量",
                        new
                        {
                            deliveryNoticeLineId = line.Id,
                            plannedQuantity = line.PlannedQuantity,
                            plannedCartons = line.PlannedCartons,
                            receivedQuantity = line.ReceivedQuantity,
                            receivedCartons = line.ReceivedCartons,
                            requestedQuantity = input.Quantity,
                            requestedCartons = input.Cartons
                        });
                }
            }

            var firstNoticeLine = noticeLines[0];
            var receiving = new ReceivingOrder
            {
                No = NumberService.NewNo("RCV"),
                PurchaseOrderId = firstNoticeLine.PurchaseOrderId,
                DeliveryNoticeId = notice.Id,
                WarehouseId = notice.WarehouseId,
                SupplierId = notice.SupplierId,
                ReceiveDate = DateTime.Today,
                Status = "received",
                TemporaryQuantity = RmbMoneyService.Round(lines.Sum(x => x.Quantity)),
                TemporaryCartons = RmbMoneyService.Round(lines.Sum(x => x.Cartons)),
                CreatedBy = userId,
                CreatedAt = DateTime.Now
            };
            _db.ReceivingOrders.Add(receiving);
            await _db.SaveChangesAsync();

            var poLineIds = noticeLines.Select(x => x.PurchaseOrderLineId).Distinct().ToList();
            var poLines = await _db.DocumentLines
                .Where(x => x.DocumentType == "PO" && poLineIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            var sortNo = 1;
            foreach (var noticeLine in noticeLines)
            {
                var input = inputById[noticeLine.Id];
                if (!poLines.TryGetValue(noticeLine.PurchaseOrderLineId, out var sourceLine))
                    throw new BusinessRuleException("PURCHASE_ORDER_LINE_NOT_FOUND", "采购订单商品明细不存在");

                noticeLine.ReceivedQuantity = RmbMoneyService.Round(noticeLine.ReceivedQuantity + input.Quantity);
                noticeLine.ReceivedCartons = RmbMoneyService.Round(noticeLine.ReceivedCartons + input.Cartons);
                noticeLine.UpdatedBy = userId;
                noticeLine.UpdatedAt = DateTime.Now;

                _db.DocumentLines.Add(new DocumentLine
                {
                    DocumentType = "RCV",
                    DocumentId = receiving.Id,
                    ProductId = sourceLine.ProductId,
                    OrderProductId = noticeLine.OrderProductId,
                    SourceDocumentLineId = sourceLine.Id,
                    DeliveryNoticeLineId = noticeLine.Id,
                    CustomerId = sourceLine.CustomerId,
                    SupplierId = notice.SupplierId,
                    WarehouseId = notice.WarehouseId,
                    Sku = sourceLine.Sku,
                    ProductName = sourceLine.ProductName,
                    Unit = sourceLine.Unit,
                    Quantity = RmbMoneyService.Round(input.Quantity),
                    Cartons = RmbMoneyService.Round(input.Cartons),
                    CartonQty = input.Cartons > 0 ? RmbMoneyService.Round(input.Quantity / input.Cartons) : sourceLine.CartonQty,
                    CartonLengthCm = sourceLine.CartonLengthCm,
                    CartonWidthCm = sourceLine.CartonWidthCm,
                    CartonHeightCm = sourceLine.CartonHeightCm,
                    CartonCbm = sourceLine.CartonCbm,
                    TotalCbm = RmbMoneyService.Round(sourceLine.CartonCbm * input.Cartons),
                    CartonGwKg = sourceLine.CartonGwKg,
                    TotalGwKg = RmbMoneyService.Round(sourceLine.CartonGwKg * input.Cartons),
                    CartonNwKg = sourceLine.CartonNwKg,
                    TotalNwKg = RmbMoneyService.Round(sourceLine.CartonNwKg * input.Cartons),
                    SupplierItemNo = sourceLine.SupplierItemNo,
                    CustomerItemNo = sourceLine.CustomerItemNo,
                    PurchaseUnitPriceSnapshot = sourceLine.PurchaseUnitPriceSnapshot,
                    SalesUnitPriceSnapshot = sourceLine.SalesUnitPriceSnapshot,
                    UnitPrice = 0m,
                    Amount = 0m,
                    Remark = input.Remark,
                    SortNo = sortNo++,
                    CreatedAt = DateTime.Now
                });
            }

            var allNoticeLines = await _db.DeliveryNoticeLines
                .Where(x => x.DeliveryNoticeId == notice.Id)
                .ToListAsync();
            notice.ReceivedQuantity = RmbMoneyService.Round(allNoticeLines.Sum(x => x.ReceivedQuantity));
            notice.ReceivedCartons = RmbMoneyService.Round(allNoticeLines.Sum(x => x.ReceivedCartons));
            notice.Status = notice.ReceivedQuantity >= notice.TotalQuantity && notice.ReceivedCartons >= notice.TotalCartons
                ? "received"
                : "partially_received";
            notice.UpdatedBy = userId;
            notice.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            await _audit.WriteAsync(
                nameof(ReceivingOrder),
                receiving.Id,
                "create_from_delivery_notice",
                null,
                new
                {
                    receiving.DeliveryNoticeId,
                    receiving.TemporaryQuantity,
                    receiving.TemporaryCartons,
                    notice.Status
                },
                "根据送货通知创建临时收货记录",
                userId);
            if (transaction is not null) await transaction.CommitAsync();
            return receiving;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync()
        => _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;
}
