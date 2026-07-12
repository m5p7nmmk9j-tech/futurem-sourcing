using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Futurem.Sourcing.Api.Services;

public sealed record ActualLoadInput(
    long InventoryReservationId,
    decimal ActualQuantity,
    decimal ActualCartons);

public sealed record ContainerConfirmationResult(
    ContainerLoad ContainerLoad,
    FinanceRecord Receivable,
    Shipment Shipment);

public sealed class ContainerConfirmationService
{
    private readonly AppDbContext _db;
    private readonly FinanceDocumentService _finance;
    private readonly AuditTrailService _audit;
    private readonly TimeProvider _timeProvider;

    public ContainerConfirmationService(
        AppDbContext db,
        FinanceDocumentService finance,
        AuditTrailService audit,
        TimeProvider timeProvider)
    {
        _db = db;
        _finance = finance;
        _audit = audit;
        _timeProvider = timeProvider;
    }

    public async Task<ContainerConfirmationResult> ConfirmAsync(
        long containerLoadId,
        IReadOnlyCollection<ActualLoadInput> actualLines,
        long? userId)
    {
        await using var transaction = await BeginTransactionIfSupportedAsync();
        try
        {
            var container = await LoadContainerForUpdateAsync(containerLoadId)
                ?? throw new KeyNotFoundException("装柜单不存在");

            if (container.Status is "confirmed" or "shipment_created" or "completed")
            {
                var existingShipment = await _db.Shipments.FirstOrDefaultAsync(x => x.ContainerLoadId == container.Id)
                    ?? throw new BusinessRuleException("SHIPMENT_NOT_FOUND", "已确认装柜单缺少对应出运单");
                var existingReceivable = await _finance.EnsureCustomerReceivableForContainerAsync(container.Id);
                if (transaction is not null) await transaction.CommitAsync();
                return new ContainerConfirmationResult(container, existingReceivable, existingShipment);
            }

            if (container.Status != "inventory_locked")
                throw new BusinessRuleException("CONTAINER_INVENTORY_NOT_LOCKED", "确认装柜前必须锁定库存");
            if (!container.CustomerId.HasValue || !container.WarehouseId.HasValue)
                throw new BusinessRuleException("CONTAINER_SOURCE_REQUIRED", "装柜单缺少客户或仓库");

            var reservations = await _db.InventoryReservations
                .Where(x => x.ContainerLoadId == container.Id && x.Status == "active")
                .OrderBy(x => x.Id)
                .ToListAsync();
            if (reservations.Count == 0)
                throw new BusinessRuleException("CONTAINER_INVENTORY_NOT_LOCKED", "装柜单没有有效库存锁定");

            var inputMap = actualLines
                .GroupBy(x => x.InventoryReservationId)
                .ToDictionary(
                    x => x.Key,
                    x => new ActualLoadInput(
                        x.Key,
                        RmbMoneyService.Round(x.Sum(y => y.ActualQuantity)),
                        RmbMoneyService.Round(x.Sum(y => y.ActualCartons))));
            if (inputMap.Keys.Except(reservations.Select(x => x.Id)).Any() ||
                reservations.Any(x => !inputMap.ContainsKey(x.Id)))
            {
                throw new BusinessRuleException("ACTUAL_LOAD_LINES_MISMATCH", "实际装柜明细必须覆盖全部有效库存锁定行");
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            if (reservations.Any(x => x.ExpiresAt <= now))
                throw new BusinessRuleException("INVENTORY_RESERVATION_EXPIRED", "库存锁定已过期，请重新锁定后再确认装柜");
            if (inputMap.Values.All(x => x.ActualQuantity <= 0m || x.ActualCartons <= 0m))
                throw new BusinessRuleException("ACTUAL_LOAD_REQUIRED", "实际装柜数量必须大于零");

            decimal totalCartons = 0m;
            decimal totalCbm = 0m;
            decimal totalGrossWeight = 0m;
            decimal totalNetWeight = 0m;
            var sourceIds = new List<long>();
            var summaryIds = new HashSet<long>();

            foreach (var reservation in reservations)
            {
                var input = inputMap[reservation.Id];
                if (input.ActualQuantity < 0m || input.ActualCartons < 0m ||
                    input.ActualQuantity > reservation.ReservedQuantity ||
                    input.ActualCartons > reservation.ReservedCartons)
                {
                    throw new BusinessRuleException(
                        "ACTUAL_LOAD_EXCEEDS_RESERVATION",
                        "实际装柜数量不能超过锁定数量",
                        new
                        {
                            reservation.Id,
                            reservation.ReservedQuantity,
                            reservation.ReservedCartons,
                            input.ActualQuantity,
                            input.ActualCartons
                        });
                }

                var lot = await LoadInventoryLotForUpdateAsync(reservation.InventoryLotId)
                    ?? throw new KeyNotFoundException($"库存批次 {reservation.InventoryLotId} 不存在");
                if (lot.CustomerId != container.CustomerId.Value)
                    throw new BusinessRuleException("CONTAINER_CUSTOMER_MISMATCH", "库存客户与装柜单客户不一致");
                if (lot.WarehouseId != container.WarehouseId.Value)
                    throw new BusinessRuleException("CONTAINER_WAREHOUSE_MISMATCH", "库存仓库与装柜单仓库不一致");
                if (lot.LockedQuantity < reservation.ReservedQuantity || lot.LockedCartons < reservation.ReservedCartons)
                    throw new BusinessRuleException("INVENTORY_LOCK_BALANCE_INVALID", "库存锁定余额不足，不能确认装柜");
                if (lot.OnHandQuantity < input.ActualQuantity || lot.OnHandCartons < input.ActualCartons)
                    throw new BusinessRuleException("INVENTORY_NOT_AVAILABLE", "实际装柜数量超过在库数量");

                lot.OnHandQuantity = RmbMoneyService.Round(lot.OnHandQuantity - input.ActualQuantity);
                lot.OnHandCartons = RmbMoneyService.Round(lot.OnHandCartons - input.ActualCartons);
                lot.LockedQuantity = Math.Max(0m, RmbMoneyService.Round(lot.LockedQuantity - reservation.ReservedQuantity));
                lot.LockedCartons = Math.Max(0m, RmbMoneyService.Round(lot.LockedCartons - reservation.ReservedCartons));
                lot.Status = lot.OnHandQuantity <= 0m && lot.OnHandCartons <= 0m ? "depleted" : "available";
                lot.UpdatedBy = userId;
                lot.UpdatedAt = now;

                reservation.Status = "consumed";
                reservation.ReleasedAt = now;
                reservation.ReleaseReason = "装柜确认，锁定已结转";
                reservation.UpdatedBy = userId;
                reservation.UpdatedAt = now;

                var source = await _db.ContainerLoadSources.FirstOrDefaultAsync(x =>
                    x.ContainerLoadId == container.Id && x.InventoryReservationId == reservation.Id);
                if (source is null)
                {
                    source = new ContainerLoadSource
                    {
                        ContainerLoadId = container.Id,
                        InventoryReservationId = reservation.Id,
                        InventoryLotId = lot.Id,
                        CustomerId = lot.CustomerId,
                        WarehouseId = lot.WarehouseId,
                        OrderProductId = lot.OrderProductId,
                        PurchaseOrderId = lot.PurchaseOrderId,
                        PurchaseOrderLineId = lot.PurchaseOrderLineId,
                        SummaryOrderId = lot.SummaryOrderId,
                        ReceivingOrderId = lot.ReceivingOrderId,
                        QcOrderId = lot.QcOrderId,
                        SupplierId = lot.SupplierId,
                        PlannedQuantity = reservation.ReservedQuantity,
                        PlannedCartons = reservation.ReservedCartons,
                        ActualQuantity = input.ActualQuantity,
                        ActualCartons = input.ActualCartons,
                        PurchaseUnitPrice = lot.PurchaseUnitPrice,
                        SalesUnitPrice = lot.SalesUnitPrice,
                        ActualCbm = Math.Round(input.ActualCartons * lot.CartonCbm, 6, MidpointRounding.AwayFromZero),
                        ActualGrossWeightKg = Math.Round(input.ActualCartons * lot.CartonGwKg, 4, MidpointRounding.AwayFromZero),
                        Status = input.ActualQuantity > 0m ? "loaded" : "not_loaded",
                        CreatedBy = userId,
                        CreatedAt = now
                    };
                    _db.ContainerLoadSources.Add(source);
                    await _db.SaveChangesAsync();
                }
                sourceIds.Add(source.Id);
                if (source.SummaryOrderId.HasValue) summaryIds.Add(source.SummaryOrderId.Value);

                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    InventoryLotId = lot.Id,
                    WarehouseId = lot.WarehouseId,
                    WarehouseLocationId = lot.WarehouseLocationId,
                    TransactionType = "container_out",
                    SourceType = "CONTAINER_LOAD",
                    SourceId = container.Id,
                    Reason = $"装柜单 {container.No} 确认实际装柜",
                    QuantityDelta = -input.ActualQuantity,
                    CartonsDelta = -input.ActualCartons,
                    QuantityBalance = lot.OnHandQuantity,
                    CartonsBalance = lot.OnHandCartons,
                    LockedQuantityBalance = lot.LockedQuantity,
                    LockedCartonsBalance = lot.LockedCartons,
                    CreatedBy = userId,
                    CreatedAt = now
                });

                totalCartons += input.ActualCartons;
                totalCbm += input.ActualCartons * lot.CartonCbm;
                totalGrossWeight += input.ActualCartons * lot.CartonGwKg;
                totalNetWeight += input.ActualCartons * lot.CartonNwKg;
            }

            foreach (var summaryId in summaryIds)
            {
                var summary = await _db.SummaryOrders.FirstOrDefaultAsync(x => x.Id == summaryId);
                if (summary is not null)
                {
                    summary.Status = "loaded";
                    summary.UpdatedAt = now;
                }
            }

            container.TotalCartons = RmbMoneyService.Round(totalCartons);
            container.TotalCbm = Math.Round(totalCbm, 6, MidpointRounding.AwayFromZero);
            container.TotalGwKg = Math.Round(totalGrossWeight, 4, MidpointRounding.AwayFromZero);
            container.LoadDate ??= now;
            container.Status = "confirmed";
            container.UpdatedBy = userId;
            container.UpdatedAt = now;
            await _db.SaveChangesAsync();

            var receivable = await _finance.EnsureCustomerReceivableForContainerAsync(container.Id);
            var shipment = await _db.Shipments.FirstOrDefaultAsync(x => x.ContainerLoadId == container.Id);
            if (shipment is null)
            {
                shipment = new Shipment
                {
                    No = NumberService.NewNo("SHP"),
                    ContainerLoadId = container.Id,
                    SummaryOrderId = container.SummaryOrderId,
                    CustomerId = container.CustomerId,
                    WarehouseId = container.WarehouseId,
                    ContainerType = container.ContainerType,
                    ContainerNo = container.ContainerNo,
                    SealNo = container.SealNo,
                    ShipmentMode = "SEA",
                    Status = "draft",
                    Currency = RmbMoneyService.Currency,
                    CalculatedTotalCbm = RmbMoneyService.Round(container.TotalCbm),
                    FinalTotalCbm = RmbMoneyService.Round(container.TotalCbm),
                    CalculatedGrossWeightKg = RmbMoneyService.Round(container.TotalGwKg),
                    FinalGrossWeightKg = RmbMoneyService.Round(container.TotalGwKg),
                    CalculatedNetWeightKg = RmbMoneyService.Round(totalNetWeight),
                    FinalNetWeightKg = RmbMoneyService.Round(totalNetWeight),
                    Remark = $"由装柜单 {container.No} 自动生成",
                    CreatedBy = userId,
                    CreatedAt = now
                };
                _db.Shipments.Add(shipment);
                await _db.SaveChangesAsync();
            }

            container.Status = "shipment_created";
            container.UpdatedAt = now;
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(
                nameof(ContainerLoad),
                container.Id,
                "confirm",
                new { status = "inventory_locked" },
                new
                {
                    container.Status,
                    container.TotalCartons,
                    container.TotalCbm,
                    container.TotalGwKg,
                    sourceIds,
                    receivableId = receivable.Id,
                    shipmentId = shipment.Id
                },
                "确认实际装柜并生成商品应收与出运草稿",
                userId);

            if (transaction is not null) await transaction.CommitAsync();
            return new ContainerConfirmationResult(container, receivable, shipment);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<ContainerLoad?> LoadContainerForUpdateAsync(long id)
    {
        if (!_db.Database.IsRelational())
            return await _db.ContainerLoads.FirstOrDefaultAsync(x => x.Id == id);
        return await _db.ContainerLoads
            .FromSqlInterpolated($"SELECT * FROM `container_loads` WHERE `id` = {id} AND `is_deleted` = 0 FOR UPDATE")
            .SingleOrDefaultAsync();
    }

    private async Task<InventoryLot?> LoadInventoryLotForUpdateAsync(long id)
    {
        if (!_db.Database.IsRelational())
            return await _db.InventoryLots.FirstOrDefaultAsync(x => x.Id == id);
        return await _db.InventoryLots
            .FromSqlInterpolated($"SELECT * FROM `inventory_lots` WHERE `id` = {id} AND `is_deleted` = 0 FOR UPDATE")
            .SingleOrDefaultAsync();
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync()
        => _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;
}
