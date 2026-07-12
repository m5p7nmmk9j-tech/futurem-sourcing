using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Tests.Services;

public class ContainerReservationServiceTests
{
    private static readonly DateTime LockTime = new(2026, 7, 11, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Lock_RejectsCustomerMismatch()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        fixture.Lot.CustomerId += 99;
        await db.SaveChangesAsync();
        var service = CreateService(db, LockTime);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.LockAsync(fixture.Container.Id, [new(fixture.Lot.Id, 20m, 2m)], 7));

        Assert.Equal("CONTAINER_CUSTOMER_MISMATCH", ex.Code);
        Assert.Equal(0m, fixture.Lot.LockedQuantity);
    }

    [Fact]
    public async Task Lock_RejectsWarehouseMismatch()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        fixture.Lot.WarehouseId += 99;
        await db.SaveChangesAsync();
        var service = CreateService(db, LockTime);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.LockAsync(fixture.Container.Id, [new(fixture.Lot.Id, 20m, 2m)], 7));

        Assert.Equal("CONTAINER_WAREHOUSE_MISMATCH", ex.Code);
    }

    [Fact]
    public async Task Lock_RejectsUnavailableInventory()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = CreateService(db, LockTime);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.LockAsync(fixture.Container.Id, [new(fixture.Lot.Id, 101m, 11m)], 7));

        Assert.Equal("INVENTORY_NOT_AVAILABLE", ex.Code);
        Assert.Empty(db.InventoryReservations);
    }

    [Fact]
    public async Task Lock_CreatesReservationAndInventoryTransactionForExactly72Hours()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = CreateService(db, LockTime);

        var result = await service.LockAsync(
            fixture.Container.Id,
            [new(fixture.Lot.Id, 40m, 4m)],
            7);

        var reservation = Assert.Single(result);
        Assert.Equal(LockTime, reservation.LockedAt);
        Assert.Equal(LockTime.AddHours(72), reservation.ExpiresAt);
        Assert.Equal("active", reservation.Status);
        Assert.Equal(40m, fixture.Lot.LockedQuantity);
        Assert.Equal(4m, fixture.Lot.LockedCartons);
        Assert.Equal("inventory_locked", fixture.Container.Status);
        Assert.Equal(reservation.ExpiresAt, fixture.Container.InventoryLockExpiresAt);
        var transaction = await db.InventoryTransactions.SingleAsync(x => x.TransactionType == "load_lock");
        Assert.Equal(40m, transaction.LockedQuantityBalance);
    }

    [Fact]
    public async Task ActiveLock_CannotBeSavedAgainToExtendExpiry()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = CreateService(db, LockTime);
        await service.LockAsync(fixture.Container.Id, [new(fixture.Lot.Id, 20m, 2m)], 7);
        var originalExpiry = fixture.Container.InventoryLockExpiresAt;

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.LockAsync(fixture.Container.Id, [new(fixture.Lot.Id, 20m, 2m)], 7));

        Assert.Equal("CONTAINER_ALREADY_LOCKED", ex.Code);
        Assert.Equal(originalExpiry, fixture.Container.InventoryLockExpiresAt);
    }

    [Fact]
    public async Task Expire_KeepsLockBeforeDeadlineAndReleasesAtDeadline()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = CreateService(db, LockTime);
        await service.LockAsync(fixture.Container.Id, [new(fixture.Lot.Id, 40m, 4m)], 7);

        var before = await service.ExpireAsync(new DateTime(2026, 7, 14, 8, 59, 0, DateTimeKind.Utc));
        Assert.Equal(0, before);
        Assert.Equal(40m, fixture.Lot.LockedQuantity);

        var atDeadline = await service.ExpireAsync(new DateTime(2026, 7, 14, 9, 0, 0, DateTimeKind.Utc));
        Assert.Equal(1, atDeadline);
        Assert.Equal(0m, fixture.Lot.LockedQuantity);
        Assert.Equal(0m, fixture.Lot.LockedCartons);
        Assert.Equal("lock_expired", fixture.Container.Status);
        Assert.Equal("expired", (await db.InventoryReservations.SingleAsync()).Status);
        Assert.Contains(await db.InventoryTransactions.ToListAsync(), x => x.TransactionType == "load_unlock");
    }

    [Fact]
    public async Task Relock_AfterExpiryStartsANew72HourWindow()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = CreateService(db, LockTime);
        await service.LockAsync(fixture.Container.Id, [new(fixture.Lot.Id, 20m, 2m)], 7);
        await service.ExpireAsync(LockTime.AddHours(72));

        var newTime = LockTime.AddHours(80);
        var relockService = CreateService(db, newTime);
        var result = await relockService.RelockAsync(
            fixture.Container.Id,
            [new(fixture.Lot.Id, 30m, 3m)],
            8);

        var reservation = Assert.Single(result);
        Assert.Equal(newTime.AddHours(72), reservation.ExpiresAt);
        Assert.Equal(30m, fixture.Lot.LockedQuantity);
        Assert.Equal("inventory_locked", fixture.Container.Status);
        Assert.Equal(2, await db.InventoryReservations.CountAsync());
    }

    private static ContainerReservationService CreateService(
        Futurem.Sourcing.Api.Data.AppDbContext db,
        DateTime now)
        => new(db, new AuditTrailService(db), new FixedTimeProvider(now));

    private static async Task<Fixture> CreateFixtureAsync(Futurem.Sourcing.Api.Data.AppDbContext db)
    {
        var customer = new Customer { Code = "C-LOCK", Name = "锁定客户", Currency = "RMB" };
        var supplier = new Supplier { Code = "S-LOCK", Name = "锁定供应商" };
        var warehouse = new Warehouse { Code = "WH-LOCK", Name = "锁定仓", Status = "active" };
        db.AddRange(customer, supplier, warehouse);
        await db.SaveChangesAsync();

        var product = new OrderProduct
        {
            CustomerId = customer.Id,
            SupplierId = supplier.Id,
            SourceCustomerOrderId = 1,
            SystemSku = "LOCK-SKU",
            CustomerBarcode = "LOCK-BAR",
            NameCn = "锁定商品",
            Unit = "PCS",
            PurchaseUnitPrice = 10m,
            SalesUnitPrice = 15m,
            CartonQty = 10m,
            ImporterProfileId = 1,
            LabelTemplateId = 1,
            MarkTemplateId = 2,
            Status = "locked"
        };
        db.OrderProducts.Add(product);
        await db.SaveChangesAsync();

        var lot = new InventoryLot
        {
            LotNo = "LOT-LOCK",
            CustomerId = customer.Id,
            OrderProductId = product.Id,
            PurchaseOrderId = 1,
            ReceivingOrderId = 1,
            ReceivingLineId = 1,
            QcOrderId = 1,
            QcOrderLineId = 1,
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            Status = "available",
            OnHandQuantity = 100m,
            OnHandCartons = 10m,
            LockedQuantity = 0m,
            LockedCartons = 0m,
            CartonQty = 10m
        };
        var container = new ContainerLoad
        {
            No = "CL-LOCK",
            CustomerId = customer.Id,
            WarehouseId = warehouse.Id,
            ContainerType = "40HQ",
            Status = "draft"
        };
        db.AddRange(lot, container);
        await db.SaveChangesAsync();
        return new Fixture(lot, container);
    }

    private sealed record Fixture(InventoryLot Lot, ContainerLoad Container);

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _value;
        public FixedTimeProvider(DateTime value) => _value = new DateTimeOffset(value);
        public override DateTimeOffset GetUtcNow() => _value;
    }
}
