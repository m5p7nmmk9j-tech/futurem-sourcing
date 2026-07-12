using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Futurem.Sourcing.Api.Services;

public sealed record InventoryAvailability(
    decimal OnHandQuantity,
    decimal LockedQuantity,
    decimal AvailableQuantity,
    decimal OnHandCartons,
    decimal LockedCartons,
    decimal AvailableCartons);

public sealed class InventoryService
{
    private readonly AppDbContext _db;
    private readonly AuditTrailService _audit;

    public InventoryService(AppDbContext db, AuditTrailService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<InventoryLot> ReceiveAcceptedAsync(
        long qcOrderLineId,
        long warehouseId,
        long? warehouseLocationId,
        long? userId)
    {
        await using var transaction = await BeginTransactionIfSupportedAsync();
        try
        {
            var warehouse = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == warehouseId)
                ?? throw new BusinessRuleException("WAREHOUSE_NOT_FOUND", "仓库不存在");
            if (warehouse.Status != "active")
                throw new BusinessRuleException("WAREHOUSE_INACTIVE", "仓库已停用");

            if (warehouseLocationId.HasValue)
            {
                var location = await _db.WarehouseLocations.FirstOrDefaultAsync(x => x.Id == warehouseLocationId.Value)
                    ?? throw new BusinessRuleException("WAREHOUSE_LOCATION_NOT_FOUND", "库位不存在");
                if (location.WarehouseId != warehouse.Id)
                    throw new BusinessRuleException("WAREHOUSE_LOCATION_MISMATCH", "所选库位不属于当前仓库");
                if (location.Status != "active")
                    throw new BusinessRuleException("WAREHOUSE_LOCATION_INACTIVE", "库位已停用");
            }

            var existing = await _db.InventoryLots.FirstOrDefaultAsync(x =>
                x.QcOrderLineId == qcOrderLineId &&
                x.WarehouseId == warehouseId &&
                x.WarehouseLocationId == warehouseLocationId);
            if (existing is not null)
            {
                if (transaction is not null) await transaction.CommitAsync();
                return existing;
            }

            var qcLine = await _db.QcOrderLines.FirstOrDefaultAsync(x => x.Id == qcOrderLineId)
                ?? throw new KeyNotFoundException("验货商品行不存在");
            var qc = await _db.QcOrders.FirstOrDefaultAsync(x => x.Id == qcLine.QcOrderId)
                ?? throw new KeyNotFoundException("验货单不存在");
            if (qc.Status != "confirmed")
                throw new BusinessRuleException("QC_NOT_CONFIRMED", "验货单确认后才能入库");
            if (qcLine.AcceptedQuantity <= 0)
                throw new BusinessRuleException("QC_ACCEPTED_QUANTITY_REQUIRED", "最终接受数量必须大于零");

            var receiving = await _db.ReceivingOrders.FirstOrDefaultAsync(x => x.Id == qcLine.ReceivingOrderId)
                ?? throw new KeyNotFoundException("收货单不存在");
            var receivingLine = await _db.DocumentLines.FirstOrDefaultAsync(x =>
                x.Id == qcLine.ReceivingLineId && x.DocumentType == "RCV" && !x.IsDeleted)
                ?? throw new KeyNotFoundException("收货商品行不存在");
            var product = await _db.OrderProducts.FirstOrDefaultAsync(x => x.Id == qcLine.OrderProductId)
                ?? throw new KeyNotFoundException("订单商品不存在");

            DeliveryNoticeLine? noticeLine = null;
            DeliveryNotice? notice = null;
            if (qcLine.DeliveryNoticeLineId.HasValue)
            {
                noticeLine = await _db.DeliveryNoticeLines.FirstOrDefaultAsync(x => x.Id == qcLine.DeliveryNoticeLineId.Value);
                if (noticeLine is not null)
                    notice = await _db.DeliveryNotices.FirstOrDefaultAsync(x => x.Id == noticeLine.DeliveryNoticeId);
            }

            var cartons = CalculateAcceptedCartons(qcLine.AcceptedQuantity, receivingLine);
            var lot = new InventoryLot
            {
                LotNo = NumberService.NewNo("LOT"),
                CustomerId = receivingLine.CustomerId ?? product.CustomerId,
                OrderProductId = product.Id,
                PurchaseOrderId = qcLine.PurchaseOrderId,
                PurchaseOrderLineId = qcLine.PurchaseOrderLineId,
                SummaryOrderId = notice?.SummaryOrderId,
                DeliveryNoticeId = notice?.Id ?? receiving.DeliveryNoticeId,
                DeliveryNoticeLineId = noticeLine?.Id ?? qcLine.DeliveryNoticeLineId,
                ReceivingOrderId = receiving.Id,
                ReceivingLineId = receivingLine.Id,
                QcOrderId = qc.Id,
                QcOrderLineId = qcLine.Id,
                SupplierId = qcLine.SupplierId,
                WarehouseId = warehouse.Id,
                WarehouseLocationId = warehouseLocationId,
                Status = "available",
                OnHandQuantity = RmbMoneyService.Round(qcLine.AcceptedQuantity),
                LockedQuantity = 0m,
                OnHandCartons = cartons,
                LockedCartons = 0m,
                CartonQty = receivingLine.CartonQty,
                CartonCbm = receivingLine.CartonCbm,
                CartonGwKg = receivingLine.CartonGwKg,
                CartonNwKg = receivingLine.CartonNwKg,
                PurchaseUnitPrice = qcLine.PurchaseUnitPrice,
                SalesUnitPrice = receivingLine.SalesUnitPriceSnapshot,
                CreatedBy = userId,
                CreatedAt = DateTime.Now
            };
            _db.InventoryLots.Add(lot);
            await _db.SaveChangesAsync();

            receivingLine.InventoryLotId = lot.Id;
            receivingLine.WarehouseId = warehouse.Id;
            receivingLine.WarehouseLocationId = warehouseLocationId;
            receivingLine.UpdatedAt = DateTime.Now;
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                InventoryLotId = lot.Id,
                WarehouseId = warehouse.Id,
                WarehouseLocationId = warehouseLocationId,
                TransactionType = "receive_accepted",
                SourceType = "QC_LINE",
                SourceId = qcLine.Id,
                Reason = "验货确认最终接受数量入库",
                QuantityDelta = lot.OnHandQuantity,
                CartonsDelta = lot.OnHandCartons,
                QuantityBalance = lot.OnHandQuantity,
                CartonsBalance = lot.OnHandCartons,
                LockedQuantityBalance = 0m,
                LockedCartonsBalance = 0m,
                CreatedBy = userId,
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(
                nameof(InventoryLot),
                lot.Id,
                "receive_accepted",
                null,
                new
                {
                    lot.QcOrderLineId,
                    lot.WarehouseId,
                    lot.WarehouseLocationId,
                    lot.OnHandQuantity,
                    lot.OnHandCartons
                },
                "验货确认入库",
                userId);

            if (transaction is not null) await transaction.CommitAsync();
            return lot;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<InventoryAvailability> GetAvailableAsync(long inventoryLotId)
    {
        var lot = await _db.InventoryLots.FirstOrDefaultAsync(x => x.Id == inventoryLotId)
            ?? throw new KeyNotFoundException("库存批次不存在");
        return new InventoryAvailability(
            lot.OnHandQuantity,
            lot.LockedQuantity,
            lot.AvailableQuantity,
            lot.OnHandCartons,
            lot.LockedCartons,
            lot.AvailableCartons);
    }

    public async Task<InventoryLot> AdjustAsync(
        long inventoryLotId,
        decimal quantityDelta,
        decimal cartonsDelta,
        string reason,
        long? userId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("INVENTORY_ADJUST_REASON_REQUIRED", "库存调整必须填写原因");
        if (quantityDelta == 0m && cartonsDelta == 0m)
            throw new BusinessRuleException("INVENTORY_ADJUSTMENT_REQUIRED", "库存调整数量不能全部为零");

        await using var transaction = await BeginTransactionIfSupportedAsync();
        try
        {
            var lot = await _db.InventoryLots.FirstOrDefaultAsync(x => x.Id == inventoryLotId)
                ?? throw new KeyNotFoundException("库存批次不存在");
            var newQuantity = RmbMoneyService.Round(lot.OnHandQuantity + quantityDelta);
            var newCartons = RmbMoneyService.Round(lot.OnHandCartons + cartonsDelta);
            if (newQuantity < 0m || newCartons < 0m ||
                newQuantity < lot.LockedQuantity || newCartons < lot.LockedCartons)
            {
                throw new BusinessRuleException(
                    "INVENTORY_BALANCE_NEGATIVE",
                    "调整后库存不能为负数，也不能低于已锁定数量",
                    new
                    {
                        lot.Id,
                        lot.OnHandQuantity,
                        lot.LockedQuantity,
                        lot.OnHandCartons,
                        lot.LockedCartons,
                        quantityDelta,
                        cartonsDelta
                    });
            }

            var before = new { lot.OnHandQuantity, lot.OnHandCartons };
            lot.OnHandQuantity = newQuantity;
            lot.OnHandCartons = newCartons;
            lot.Status = newQuantity == 0m && newCartons == 0m ? "depleted" : "available";
            lot.UpdatedBy = userId;
            lot.UpdatedAt = DateTime.Now;
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                InventoryLotId = lot.Id,
                WarehouseId = lot.WarehouseId,
                WarehouseLocationId = lot.WarehouseLocationId,
                TransactionType = "adjust",
                SourceType = "MANUAL_ADJUSTMENT",
                SourceId = lot.Id,
                Reason = reason.Trim(),
                QuantityDelta = RmbMoneyService.Round(quantityDelta),
                CartonsDelta = RmbMoneyService.Round(cartonsDelta),
                QuantityBalance = lot.OnHandQuantity,
                CartonsBalance = lot.OnHandCartons,
                LockedQuantityBalance = lot.LockedQuantity,
                LockedCartonsBalance = lot.LockedCartons,
                CreatedBy = userId,
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(
                nameof(InventoryLot),
                lot.Id,
                "adjust",
                before,
                new { lot.OnHandQuantity, lot.OnHandCartons },
                reason,
                userId);
            if (transaction is not null) await transaction.CommitAsync();
            return lot;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    private static decimal CalculateAcceptedCartons(decimal acceptedQuantity, DocumentLine receivingLine)
    {
        if (receivingLine.CartonQty > 0m)
            return RmbMoneyService.Round(acceptedQuantity / receivingLine.CartonQty);
        if (receivingLine.Quantity > 0m && receivingLine.Cartons > 0m)
            return RmbMoneyService.Round(acceptedQuantity / receivingLine.Quantity * receivingLine.Cartons);
        return 0m;
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync()
        => _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;
}
