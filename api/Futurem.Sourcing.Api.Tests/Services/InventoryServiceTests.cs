using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Tests.Services;

public class InventoryServiceTests
{
    [Fact]
    public async Task ReceiveAccepted_CreatesTraceableLotAndTransaction()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = new InventoryService(db, new AuditTrailService(db));

        var lot = await service.ReceiveAcceptedAsync(
            fixture.QcLine.Id,
            fixture.Warehouse.Id,
            fixture.Location.Id,
            7);

        Assert.Equal(fixture.Customer.Id, lot.CustomerId);
        Assert.Equal(fixture.Product.Id, lot.OrderProductId);
        Assert.Equal(fixture.PurchaseOrder.Id, lot.PurchaseOrderId);
        Assert.Equal(fixture.PurchaseOrderLine.Id, lot.PurchaseOrderLineId);
        Assert.Equal(fixture.Summary.Id, lot.SummaryOrderId);
        Assert.Equal(fixture.DeliveryNotice.Id, lot.DeliveryNoticeId);
        Assert.Equal(fixture.DeliveryNoticeLine.Id, lot.DeliveryNoticeLineId);
        Assert.Equal(fixture.Receiving.Id, lot.ReceivingOrderId);
        Assert.Equal(fixture.ReceivingLine.Id, lot.ReceivingLineId);
        Assert.Equal(fixture.Qc.Id, lot.QcOrderId);
        Assert.Equal(fixture.QcLine.Id, lot.QcOrderLineId);
        Assert.Equal(fixture.Supplier.Id, lot.SupplierId);
        Assert.Equal(fixture.Warehouse.Id, lot.WarehouseId);
        Assert.Equal(fixture.Location.Id, lot.WarehouseLocationId);
        Assert.Equal(82m, lot.OnHandQuantity);
        Assert.Equal(4.1m, lot.OnHandCartons);
        Assert.Equal(0m, lot.LockedQuantity);
        Assert.Equal(82m, lot.AvailableQuantity);
        Assert.Equal(4.1m, lot.AvailableCartons);

        var transaction = Assert.Single(await db.InventoryTransactions.ToListAsync());
        Assert.Equal("receive_accepted", transaction.TransactionType);
        Assert.Equal(82m, transaction.QuantityDelta);
        Assert.Equal(4.1m, transaction.CartonsDelta);
        Assert.Equal(82m, transaction.QuantityBalance);
        Assert.Equal(fixture.QcLine.Id, transaction.SourceId);
        Assert.Equal("QC_LINE", transaction.SourceType);
    }

    [Fact]
    public async Task ReceiveAccepted_IsIdempotentForQcLineWarehouseAndLocation()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = new InventoryService(db, new AuditTrailService(db));

        var first = await service.ReceiveAcceptedAsync(fixture.QcLine.Id, fixture.Warehouse.Id, fixture.Location.Id, 7);
        var second = await service.ReceiveAcceptedAsync(fixture.QcLine.Id, fixture.Warehouse.Id, fixture.Location.Id, 8);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await db.InventoryLots.ToListAsync());
        Assert.Single(await db.InventoryTransactions.ToListAsync());
    }

    [Fact]
    public async Task Adjust_UpdatesBalanceAndWritesReasonedTransaction()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = new InventoryService(db, new AuditTrailService(db));
        var lot = await service.ReceiveAcceptedAsync(fixture.QcLine.Id, fixture.Warehouse.Id, fixture.Location.Id, 7);

        var adjusted = await service.AdjustAsync(lot.Id, -2m, -0.1m, "仓库复点差异", 9);

        Assert.Equal(80m, adjusted.OnHandQuantity);
        Assert.Equal(4m, adjusted.OnHandCartons);
        Assert.Equal(80m, adjusted.AvailableQuantity);
        var transaction = (await db.InventoryTransactions.OrderBy(x => x.Id).ToListAsync()).Last();
        Assert.Equal("adjust", transaction.TransactionType);
        Assert.Equal(-2m, transaction.QuantityDelta);
        Assert.Equal(-0.1m, transaction.CartonsDelta);
        Assert.Equal("仓库复点差异", transaction.Reason);
        Assert.Equal(80m, transaction.QuantityBalance);
    }

    [Fact]
    public async Task Adjust_RejectsBlankReasonAndNegativeOrBelowLockedBalance()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = new InventoryService(db, new AuditTrailService(db));
        var lot = await service.ReceiveAcceptedAsync(fixture.QcLine.Id, fixture.Warehouse.Id, fixture.Location.Id, 7);

        var reasonEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.AdjustAsync(lot.Id, -1m, 0m, "", 9));
        Assert.Equal("INVENTORY_ADJUST_REASON_REQUIRED", reasonEx.Code);

        lot.LockedQuantity = 80m;
        lot.LockedCartons = 4m;
        await db.SaveChangesAsync();
        var balanceEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.AdjustAsync(lot.Id, -3m, -0.2m, "错误调整", 9));
        Assert.Equal("INVENTORY_BALANCE_NEGATIVE", balanceEx.Code);
        Assert.Equal(82m, lot.OnHandQuantity);
    }

    [Fact]
    public async Task ReceiveAccepted_RejectsLocationFromAnotherWarehouse()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var otherWarehouse = new Warehouse { Code = "WH-2", Name = "二号仓", Status = "active" };
        db.Warehouses.Add(otherWarehouse);
        await db.SaveChangesAsync();
        var otherLocation = new WarehouseLocation
        {
            WarehouseId = otherWarehouse.Id,
            Code = "B-01",
            Name = "B区01",
            Status = "active"
        };
        db.WarehouseLocations.Add(otherLocation);
        await db.SaveChangesAsync();
        var service = new InventoryService(db, new AuditTrailService(db));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ReceiveAcceptedAsync(fixture.QcLine.Id, fixture.Warehouse.Id, otherLocation.Id, 7));

        Assert.Equal("WAREHOUSE_LOCATION_MISMATCH", ex.Code);
        Assert.Empty(await db.InventoryLots.ToListAsync());
    }

    private static async Task<Fixture> CreateFixtureAsync(AppDbContext db)
    {
        var customer = new Customer { Code = "C-INV", Name = "库存客户", Currency = "RMB" };
        var supplier = new Supplier { Code = "S-INV", Name = "库存供应商" };
        var warehouse = new Warehouse { Code = "WH-1", Name = "一号仓", Status = "active" };
        db.AddRange(customer, supplier, warehouse);
        await db.SaveChangesAsync();
        var location = new WarehouseLocation
        {
            WarehouseId = warehouse.Id,
            Code = "A-01",
            Name = "A区01",
            Status = "active"
        };
        db.WarehouseLocations.Add(location);
        await db.SaveChangesAsync();

        var co = new CustomerOrder { No = "CO-INV", CustomerId = customer.Id, Status = "confirmed", Currency = "RMB" };
        db.CustomerOrders.Add(co);
        await db.SaveChangesAsync();
        var product = new OrderProduct
        {
            CustomerId = customer.Id,
            SupplierId = supplier.Id,
            SourceCustomerOrderId = co.Id,
            SystemSku = "SKU-INV",
            CustomerBarcode = "BAR-INV",
            NameCn = "库存商品",
            Unit = "PCS",
            PurchaseUnitPrice = 10m,
            SalesUnitPrice = 15m,
            CartonQty = 20m,
            CartonCbm = 0.5m,
            CartonGwKg = 12m,
            CartonNwKg = 10m,
            ImporterProfileId = 1,
            LabelTemplateId = 1,
            MarkTemplateId = 2,
            Status = "locked"
        };
        db.OrderProducts.Add(product);
        await db.SaveChangesAsync();
        var po = new PurchaseOrder
        {
            No = "PO-INV",
            CustomerOrderId = co.Id,
            CustomerId = customer.Id,
            SupplierId = supplier.Id,
            Status = "confirmed",
            Currency = "RMB"
        };
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();
        var poLine = new DocumentLine
        {
            DocumentType = "PO", DocumentId = po.Id, OrderProductId = product.Id,
            CustomerId = customer.Id, SupplierId = supplier.Id, Sku = product.SystemSku,
            ProductName = product.NameCn, Unit = "PCS", Quantity = 100m, CartonQty = 20m,
            Cartons = 5m, CartonCbm = 0.5m, CartonGwKg = 12m, CartonNwKg = 10m,
            PurchaseUnitPriceSnapshot = 10m, SalesUnitPriceSnapshot = 15m, UnitPrice = 10m, Amount = 1000m
        };
        db.DocumentLines.Add(poLine);
        await db.SaveChangesAsync();

        var summary = new SummaryOrder { No = "SUM-INV", CustomerId = customer.Id, Status = "confirmed", Currency = "RMB" };
        db.SummaryOrders.Add(summary);
        await db.SaveChangesAsync();
        var summaryItem = new SummaryOrderItem
        {
            SummaryOrderId = summary.Id, PurchaseOrderId = po.Id, PurchaseOrderLineId = poLine.Id,
            OrderProductId = product.Id, SupplierId = supplier.Id, ReservedCartons = 5m,
            ReservedQuantity = 100m, ReservationStatus = "confirmed"
        };
        db.SummaryOrderItems.Add(summaryItem);
        await db.SaveChangesAsync();

        var notice = new DeliveryNotice
        {
            No = "DN-INV", SourceKey = "dn-inv", SummaryOrderId = summary.Id,
            SupplierId = supplier.Id, WarehouseId = warehouse.Id, PlannedDeliveryDate = DateTime.Today,
            Status = "received", TotalQuantity = 100m, TotalCartons = 5m,
            ReceivedQuantity = 100m, ReceivedCartons = 5m
        };
        db.DeliveryNotices.Add(notice);
        await db.SaveChangesAsync();
        var noticeLine = new DeliveryNoticeLine
        {
            DeliveryNoticeId = notice.Id, SummaryOrderItemId = summaryItem.Id,
            PurchaseOrderId = po.Id, PurchaseOrderLineId = poLine.Id, OrderProductId = product.Id,
            PlannedQuantity = 100m, PlannedCartons = 5m, ReceivedQuantity = 100m, ReceivedCartons = 5m
        };
        db.DeliveryNoticeLines.Add(noticeLine);
        await db.SaveChangesAsync();

        var receiving = new ReceivingOrder
        {
            No = "RCV-INV", PurchaseOrderId = po.Id, DeliveryNoticeId = notice.Id,
            WarehouseId = warehouse.Id, SupplierId = supplier.Id, Status = "qc_confirmed",
            TemporaryQuantity = 100m, TemporaryCartons = 5m
        };
        db.ReceivingOrders.Add(receiving);
        await db.SaveChangesAsync();
        var receivingLine = new DocumentLine
        {
            DocumentType = "RCV", DocumentId = receiving.Id, OrderProductId = product.Id,
            SourceDocumentLineId = poLine.Id, DeliveryNoticeLineId = noticeLine.Id,
            CustomerId = customer.Id, SupplierId = supplier.Id, WarehouseId = warehouse.Id,
            Sku = product.SystemSku, ProductName = product.NameCn, Unit = "PCS",
            Quantity = 100m, CartonQty = 20m, Cartons = 5m,
            CartonCbm = 0.5m, CartonGwKg = 12m, CartonNwKg = 10m,
            PurchaseUnitPriceSnapshot = 10m, SalesUnitPriceSnapshot = 15m
        };
        db.DocumentLines.Add(receivingLine);
        await db.SaveChangesAsync();

        var qc = new QcOrder
        {
            No = "QC-INV", PurchaseOrderId = po.Id, ReceivingOrderId = receiving.Id,
            Status = "confirmed", Result = "accepted_partial", ConfirmationVersion = 1,
            ConfirmedAt = DateTime.Now
        };
        db.QcOrders.Add(qc);
        await db.SaveChangesAsync();
        var qcLine = new QcOrderLine
        {
            QcOrderId = qc.Id, ReceivingOrderId = receiving.Id, ReceivingLineId = receivingLine.Id,
            DeliveryNoticeLineId = noticeLine.Id, PurchaseOrderId = po.Id,
            PurchaseOrderLineId = poLine.Id, OrderProductId = product.Id,
            SupplierId = supplier.Id, WarehouseId = warehouse.Id, ConfirmationVersion = 1,
            ArrivedQuantity = 100m, QualifiedQuantity = 82m, UnqualifiedQuantity = 8m,
            ReturnedQuantity = 5m, PendingQuantity = 5m, AcceptedQuantity = 82m,
            PurchaseUnitPrice = 10m, PayableAmount = 820m
        };
        db.QcOrderLines.Add(qcLine);
        await db.SaveChangesAsync();

        return new Fixture(customer, supplier, warehouse, location, product, po, poLine,
            summary, notice, noticeLine, receiving, receivingLine, qc, qcLine);
    }

    private sealed record Fixture(
        Customer Customer, Supplier Supplier, Warehouse Warehouse, WarehouseLocation Location,
        OrderProduct Product, PurchaseOrder PurchaseOrder, DocumentLine PurchaseOrderLine,
        SummaryOrder Summary, DeliveryNotice DeliveryNotice, DeliveryNoticeLine DeliveryNoticeLine,
        ReceivingOrder Receiving, DocumentLine ReceivingLine, QcOrder Qc, QcOrderLine QcLine);
}
