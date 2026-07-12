using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Tests.Services;

public class ContainerConfirmationServiceTests
{
    private static readonly DateTime Now = new(2026, 7, 12, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Confirm_DeductsActualQuantityAndCreatesReceivableAndShipmentOnce()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var reservationService = new ContainerReservationService(db, new AuditTrailService(db), new FixedTimeProvider(Now));
        var reservation = Assert.Single(await reservationService.LockAsync(
            fixture.Container.Id,
            [new(fixture.Lot.Id, 100m, 10m)],
            7));
        var service = CreateConfirmationService(db);

        var result = await service.ConfirmAsync(
            fixture.Container.Id,
            [new ActualLoadInput(reservation.Id, 70m, 7m)],
            7);
        var second = await service.ConfirmAsync(
            fixture.Container.Id,
            [new ActualLoadInput(reservation.Id, 70m, 7m)],
            7);

        Assert.Equal(result.Shipment.Id, second.Shipment.Id);
        Assert.Equal("shipment_created", fixture.Container.Status);
        Assert.Equal(30m, fixture.Lot.OnHandQuantity);
        Assert.Equal(3m, fixture.Lot.OnHandCartons);
        Assert.Equal(0m, fixture.Lot.LockedQuantity);
        Assert.Equal(0m, fixture.Lot.LockedCartons);
        Assert.Equal("consumed", reservation.Status);
        Assert.Equal(1, await db.Shipments.CountAsync());
        Assert.Equal(1, await db.FinanceRecords.CountAsync(x => x.SourceKey == $"container:{fixture.Container.Id}:goods"));
        var finance = await db.FinanceRecords.SingleAsync(x => x.SourceKey == $"container:{fixture.Container.Id}:goods");
        Assert.Equal(1050m, finance.Amount);
        var line = await db.FinanceRecordLines.SingleAsync();
        Assert.Equal("goods", line.LineType);
        Assert.Equal(70m, line.Quantity);
        Assert.Equal(15m, line.UnitPrice);
        Assert.Equal(1050m, line.Amount);
        Assert.Equal(1, await db.ContainerLoadSources.CountAsync());
        Assert.Equal(1, await db.InventoryTransactions.CountAsync(x => x.TransactionType == "container_out"));
    }

    [Fact]
    public async Task Confirm_LeavesUnloadedInventoryAvailableAndMarksOriginalSummaryLoaded()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var reservationService = new ContainerReservationService(db, new AuditTrailService(db), new FixedTimeProvider(Now));
        var reservation = Assert.Single(await reservationService.LockAsync(
            fixture.Container.Id,
            [new(fixture.Lot.Id, 100m, 10m)],
            7));
        var service = CreateConfirmationService(db);

        await service.ConfirmAsync(
            fixture.Container.Id,
            [new ActualLoadInput(reservation.Id, 60m, 6m)],
            7);

        Assert.Equal(40m, fixture.Lot.AvailableQuantity);
        Assert.Equal(4m, fixture.Lot.AvailableCartons);
        Assert.Equal("available", fixture.Lot.Status);
        Assert.Equal("loaded", fixture.Summary.Status);
        var source = await db.ContainerLoadSources.SingleAsync();
        Assert.Equal(fixture.Summary.Id, source.SummaryOrderId);
        Assert.Equal(100m, source.PlannedQuantity);
        Assert.Equal(60m, source.ActualQuantity);
    }

    [Fact]
    public async Task Confirm_RejectsActualQuantityAboveReservation()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var reservationService = new ContainerReservationService(db, new AuditTrailService(db), new FixedTimeProvider(Now));
        var reservation = Assert.Single(await reservationService.LockAsync(
            fixture.Container.Id,
            [new(fixture.Lot.Id, 50m, 5m)],
            7));
        var service = CreateConfirmationService(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ConfirmAsync(
                fixture.Container.Id,
                [new ActualLoadInput(reservation.Id, 60m, 6m)],
                7));

        Assert.Equal("ACTUAL_LOAD_EXCEEDS_RESERVATION", ex.Code);
        Assert.Equal(100m, fixture.Lot.OnHandQuantity);
    }

    [Fact]
    public async Task Confirm_RejectsExpiredReservation()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var reservationService = new ContainerReservationService(db, new AuditTrailService(db), new FixedTimeProvider(Now));
        var reservation = Assert.Single(await reservationService.LockAsync(
            fixture.Container.Id,
            [new(fixture.Lot.Id, 50m, 5m)],
            7));
        reservation.ExpiresAt = Now.AddMinutes(-1);
        await db.SaveChangesAsync();
        var service = CreateConfirmationService(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ConfirmAsync(
                fixture.Container.Id,
                [new ActualLoadInput(reservation.Id, 50m, 5m)],
                7));

        Assert.Equal("INVENTORY_RESERVATION_EXPIRED", ex.Code);
    }

    private static ContainerConfirmationService CreateConfirmationService(Futurem.Sourcing.Api.Data.AppDbContext db)
    {
        var audit = new AuditTrailService(db);
        var finance = new FinanceDocumentService(db);
        return new ContainerConfirmationService(db, finance, audit, new FixedTimeProvider(Now));
    }

    private static async Task<Fixture> CreateFixtureAsync(Futurem.Sourcing.Api.Data.AppDbContext db)
    {
        var customer = new Customer { Code = "CC", Name = "装柜客户", Currency = "RMB" };
        var supplier = new Supplier { Code = "CS", Name = "装柜供应商" };
        var warehouse = new Warehouse { Code = "CW", Name = "装柜仓", Status = "active" };
        db.AddRange(customer, supplier, warehouse);
        await db.SaveChangesAsync();
        var product = new OrderProduct
        {
            CustomerId = customer.Id,
            SupplierId = supplier.Id,
            SourceCustomerOrderId = 1,
            SystemSku = "C-SKU",
            CustomerBarcode = "C-BAR",
            NameCn = "装柜商品",
            Unit = "PCS",
            PurchaseUnitPrice = 10m,
            SalesUnitPrice = 15m,
            CartonQty = 10m,
            CartonCbm = 1m,
            CartonGwKg = 100m,
            ImporterProfileId = 1,
            LabelTemplateId = 1,
            MarkTemplateId = 2,
            Status = "locked"
        };
        db.OrderProducts.Add(product);
        var summary = new SummaryOrder { No = "SUM-C", CustomerId = customer.Id, Currency = "RMB", Status = "ready_to_load" };
        db.SummaryOrders.Add(summary);
        await db.SaveChangesAsync();
        var lot = new InventoryLot
        {
            LotNo = "LOT-C",
            CustomerId = customer.Id,
            OrderProductId = product.Id,
            PurchaseOrderId = 11,
            PurchaseOrderLineId = 12,
            SummaryOrderId = summary.Id,
            DeliveryNoticeId = 13,
            ReceivingOrderId = 14,
            ReceivingLineId = 15,
            QcOrderId = 16,
            QcOrderLineId = 17,
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            Status = "available",
            OnHandQuantity = 100m,
            OnHandCartons = 10m,
            CartonQty = 10m,
            CartonCbm = 1m,
            CartonGwKg = 100m,
            PurchaseUnitPrice = 10m,
            SalesUnitPrice = 15m
        };
        var container = new ContainerLoad
        {
            No = "CL-C",
            CustomerId = customer.Id,
            WarehouseId = warehouse.Id,
            ContainerType = "40HQ",
            ContainerNo = "CONT-001",
            SealNo = "SEAL-001",
            Status = "draft"
        };
        db.AddRange(lot, container);
        await db.SaveChangesAsync();
        return new Fixture(lot, container, summary);
    }

    private sealed record Fixture(InventoryLot Lot, ContainerLoad Container, SummaryOrder Summary);

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _value;
        public FixedTimeProvider(DateTime value) => _value = new DateTimeOffset(value);
        public override DateTimeOffset GetUtcNow() => _value;
    }
}
