using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Futurem.Sourcing.Api.Services;

public sealed record InventoryReservationInput(
    long InventoryLotId,
    decimal Quantity,
    decimal Cartons);

public sealed class ContainerReservationService
{
    private readonly AppDbContext _db;
    private readonly AuditTrailService _audit;
    private readonly TimeProvider _timeProvider;

    public ContainerReservationService(
        AppDbContext db,
        AuditTrailService audit,
        TimeProvider timeProvider)
    {
        _db = db;
        _audit = audit;
        _timeProvider = timeProvider;
    }

    public Task<IReadOnlyCollection<InventoryReservation>> LockAsync(
        long containerLoadId,
        IReadOnlyCollection<InventoryReservationInput> items,
        long? userId)
        => LockCoreAsync(containerLoadId, items, userId, allowExpiredStatus: false);

    public Task<IReadOnlyCollection<InventoryReservation>> RelockAsync(
        long containerLoadId,
        IReadOnlyCollection<InventoryReservationInput> items,
        long? userId)
        => LockCoreAsync(containerLoadId, items, userId, allowExpiredStatus: true);

    public async Task<int> ReleaseAsync(long containerLoadId, string reason, long? userId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("CONTAINER_RELEASE_REASON_REQUIRED", "释放装柜库存必须填写原因");

        await using var transaction = await BeginTransactionIfSupportedAsync();
        try
        {
            var container = await LoadContainerForUpdateAsync(containerLoadId)
                ?? throw new KeyNotFoundException("装柜单不存在");
            if (container.Status is "confirmed" or "completed" or "shipment_created")
                throw new BusinessRuleException("CONTAINER_LOCK_RELEASE_FORBIDDEN", "已确认装柜单不能释放库存锁定");

            var active = await _db.InventoryReservations
                .Where(x => x.ContainerLoadId == containerLoadId && x.Status == "active")
                .OrderBy(x => x.Id)
                .ToListAsync();
            if (active.Count == 0)
            {
                if (transaction is not null) await transaction.CommitAsync();
                return 0;
            }

            var now = UtcNow();
            await ReleaseReservationsCoreAsync(active, "released", reason.Trim(), now, userId);
            container.Status = "draft";
            container.InventoryLockedAt = null;
            container.InventoryLockExpiresAt = null;
            container.UpdatedBy = userId;
            container.UpdatedAt = now;
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(
                nameof(ContainerLoad),
                container.Id,
                "release_inventory",
                null,
                new { releasedCount = active.Count },
                reason,
                userId);

            if (transaction is not null) await transaction.CommitAsync();
            return active.Count;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<int> ExpireAsync(DateTime now)
    {
        var normalizedNow = NormalizeUtc(now);
        await using var transaction = await BeginTransactionIfSupportedAsync();
        try
        {
            var expired = await _db.InventoryReservations
                .Where(x => x.Status == "active" && x.ExpiresAt <= normalizedNow)
                .OrderBy(x => x.ContainerLoadId)
                .ThenBy(x => x.Id)
                .ToListAsync();
            if (expired.Count == 0)
            {
                if (transaction is not null) await transaction.CommitAsync();
                return 0;
            }

            foreach (var group in expired.GroupBy(x => x.ContainerLoadId))
            {
                await ReleaseReservationsCoreAsync(
                    group.ToList(),
                    "expired",
                    "装柜草稿库存锁定已超过72小时",
                    normalizedNow,
                    null);

                var container = await LoadContainerForUpdateAsync(group.Key);
                if (container is not null && container.Status is not ("confirmed" or "completed" or "shipment_created" or "cancelled"))
                {
                    container.Status = "lock_expired";
                    container.UpdatedAt = normalizedNow;
                }
            }

            await _db.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();
            return expired.Count;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<IReadOnlyCollection<InventoryReservation>> LockCoreAsync(
        long containerLoadId,
        IReadOnlyCollection<InventoryReservationInput> items,
        long? userId,
        bool allowExpiredStatus)
    {
        if (items.Count == 0)
            throw new BusinessRuleException("CONTAINER_INVENTORY_REQUIRED", "请选择要锁定的库存");

        var normalizedItems = items
            .GroupBy(x => x.InventoryLotId)
            .Select(x => new InventoryReservationInput(
                x.Key,
                RmbMoneyService.Round(x.Sum(y => y.Quantity)),
                RmbMoneyService.Round(x.Sum(y => y.Cartons))))
            .ToList();
        if (normalizedItems.Any(x => x.InventoryLotId <= 0 || x.Quantity <= 0m || x.Cartons <= 0m))
            throw new BusinessRuleException("CONTAINER_INVENTORY_QUANTITY_INVALID", "锁定数量和箱数必须大于零");

        await using var transaction = await BeginTransactionIfSupportedAsync();
        try
        {
            var container = await LoadContainerForUpdateAsync(containerLoadId)
                ?? throw new KeyNotFoundException("装柜单不存在");
            if (!container.CustomerId.HasValue || container.CustomerId.Value <= 0)
                throw new BusinessRuleException("CONTAINER_CUSTOMER_REQUIRED", "装柜单必须先选择客户");
            if (!container.WarehouseId.HasValue || container.WarehouseId.Value <= 0)
                throw new BusinessRuleException("CONTAINER_WAREHOUSE_REQUIRED", "装柜单必须先选择仓库");
            var allowedStatuses = allowExpiredStatus
                ? new[] { "draft", "lock_expired" }
                : new[] { "draft" };
            if (!allowedStatuses.Contains(container.Status))
            {
                if (container.Status == "inventory_locked")
                    throw new BusinessRuleException("CONTAINER_ALREADY_LOCKED", "装柜单库存已经锁定，普通保存不会延长锁定时间");
                throw new BusinessRuleException("CONTAINER_STATUS_INVALID", "当前装柜单状态不能锁定库存");
            }

            var hasActive = await _db.InventoryReservations
                .AnyAsync(x => x.ContainerLoadId == container.Id && x.Status == "active");
            if (hasActive)
                throw new BusinessRuleException("CONTAINER_ALREADY_LOCKED", "装柜单库存已经锁定，普通保存不会延长锁定时间");

            var now = UtcNow();
            var expiresAt = now.AddHours(72);
            var reservations = new List<InventoryReservation>();
            decimal totalCartons = 0m;
            decimal totalCbm = 0m;
            decimal totalWeight = 0m;

            foreach (var input in normalizedItems)
            {
                var lot = await LoadInventoryLotForUpdateAsync(input.InventoryLotId)
                    ?? throw new KeyNotFoundException($"库存批次 {input.InventoryLotId} 不存在");
                if (lot.CustomerId != container.CustomerId.Value)
                    throw new BusinessRuleException(
                        "CONTAINER_CUSTOMER_MISMATCH",
                        "库存客户与装柜单客户不一致",
                        new { lot.Id, lot.CustomerId, containerCustomerId = container.CustomerId });
                if (lot.WarehouseId != container.WarehouseId.Value)
                    throw new BusinessRuleException(
                        "CONTAINER_WAREHOUSE_MISMATCH",
                        "库存仓库与装柜单仓库不一致",
                        new { lot.Id, lot.WarehouseId, containerWarehouseId = container.WarehouseId });
                if (lot.Status == "depleted" ||
                    lot.AvailableQuantity < input.Quantity ||
                    lot.AvailableCartons < input.Cartons)
                {
                    throw new BusinessRuleException(
                        "INVENTORY_NOT_AVAILABLE",
                        "部分库存已被占用或可用数量不足",
                        new
                        {
                            lot.Id,
                            requestedQuantity = input.Quantity,
                            requestedCartons = input.Cartons,
                            availableQuantity = lot.AvailableQuantity,
                            availableCartons = lot.AvailableCartons
                        });
                }

                lot.LockedQuantity = RmbMoneyService.Round(lot.LockedQuantity + input.Quantity);
                lot.LockedCartons = RmbMoneyService.Round(lot.LockedCartons + input.Cartons);
                lot.UpdatedBy = userId;
                lot.UpdatedAt = now;

                var reservation = new InventoryReservation
                {
                    ContainerLoadId = container.Id,
                    InventoryLotId = lot.Id,
                    CustomerId = lot.CustomerId,
                    WarehouseId = lot.WarehouseId,
                    ReservedQuantity = input.Quantity,
                    ReservedCartons = input.Cartons,
                    Status = "active",
                    LockedAt = now,
                    ExpiresAt = expiresAt,
                    CreatedBy = userId,
                    CreatedAt = now
                };
                _db.InventoryReservations.Add(reservation);
                reservations.Add(reservation);
                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    InventoryLotId = lot.Id,
                    WarehouseId = lot.WarehouseId,
                    WarehouseLocationId = lot.WarehouseLocationId,
                    TransactionType = "load_lock",
                    SourceType = "CONTAINER_LOAD",
                    SourceId = container.Id,
                    Reason = $"装柜单 {container.No} 锁定库存",
                    QuantityDelta = 0m,
                    CartonsDelta = 0m,
                    QuantityBalance = lot.OnHandQuantity,
                    CartonsBalance = lot.OnHandCartons,
                    LockedQuantityBalance = lot.LockedQuantity,
                    LockedCartonsBalance = lot.LockedCartons,
                    CreatedBy = userId,
                    CreatedAt = now
                });

                totalCartons += input.Cartons;
                totalCbm += input.Cartons * lot.CartonCbm;
                totalWeight += input.Cartons * lot.CartonGwKg;
            }

            container.Status = "inventory_locked";
            container.InventoryLockedAt = now;
            container.InventoryLockExpiresAt = expiresAt;
            container.TotalCartons = RmbMoneyService.Round(totalCartons);
            container.TotalCbm = Math.Round(totalCbm, 6, MidpointRounding.AwayFromZero);
            container.TotalGwKg = Math.Round(totalWeight, 4, MidpointRounding.AwayFromZero);
            container.UpdatedBy = userId;
            container.UpdatedAt = now;
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(
                nameof(ContainerLoad),
                container.Id,
                allowExpiredStatus ? "relock_inventory" : "lock_inventory",
                null,
                new
                {
                    reservationIds = reservations.Select(x => x.Id).ToArray(),
                    container.InventoryLockedAt,
                    container.InventoryLockExpiresAt
                },
                allowExpiredStatus ? "重新锁定装柜库存" : "锁定装柜库存",
                userId);

            if (transaction is not null) await transaction.CommitAsync();
            return reservations;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task ReleaseReservationsCoreAsync(
        IReadOnlyCollection<InventoryReservation> reservations,
        string targetStatus,
        string reason,
        DateTime now,
        long? userId)
    {
        foreach (var reservation in reservations)
        {
            if (reservation.Status != "active") continue;
            var lot = await LoadInventoryLotForUpdateAsync(reservation.InventoryLotId)
                ?? throw new KeyNotFoundException($"库存批次 {reservation.InventoryLotId} 不存在");
            lot.LockedQuantity = Math.Max(0m, RmbMoneyService.Round(lot.LockedQuantity - reservation.ReservedQuantity));
            lot.LockedCartons = Math.Max(0m, RmbMoneyService.Round(lot.LockedCartons - reservation.ReservedCartons));
            lot.UpdatedBy = userId;
            lot.UpdatedAt = now;
            reservation.Status = targetStatus;
            reservation.ReleasedAt = now;
            reservation.ReleaseReason = reason;
            reservation.UpdatedBy = userId;
            reservation.UpdatedAt = now;

            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                InventoryLotId = lot.Id,
                WarehouseId = lot.WarehouseId,
                WarehouseLocationId = lot.WarehouseLocationId,
                TransactionType = "load_unlock",
                SourceType = "CONTAINER_LOAD",
                SourceId = reservation.ContainerLoadId,
                Reason = reason,
                QuantityDelta = 0m,
                CartonsDelta = 0m,
                QuantityBalance = lot.OnHandQuantity,
                CartonsBalance = lot.OnHandCartons,
                LockedQuantityBalance = lot.LockedQuantity,
                LockedCartonsBalance = lot.LockedCartons,
                CreatedBy = userId,
                CreatedAt = now
            });
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

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static DateTime NormalizeUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync()
        => _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;
}
