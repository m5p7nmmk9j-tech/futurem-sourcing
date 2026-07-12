using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Tests.Services;

public class SummaryReservationServiceTests
{
    [Fact]
    public async Task Reserve_RejectsFractionalCartons()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db, poCartons: 10m);
        var service = new SummaryReservationService(db, new AuditTrailService(db));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ReserveAsync(fixture.Summary1.Id, fixture.PoLine.Id, 1.5m, 7));

        Assert.Equal("SUMMARY_WHOLE_CARTONS_REQUIRED", ex.Code);
        Assert.Empty(db.SummaryOrderItems);
    }

    [Fact]
    public async Task Reserve_CalculatesQuantityAndTotalsFromWholeCartons()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db, poCartons: 10m);
        var service = new SummaryReservationService(db, new AuditTrailService(db));

        var allocation = await service.ReserveAsync(
            fixture.Summary1.Id,
            fixture.PoLine.Id,
            4m,
            7);

        Assert.Equal(4m, allocation.ReservedCartons);
        Assert.Equal(80m, allocation.ReservedQuantity);
        Assert.Equal("draft_reserved", allocation.ReservationStatus);
        Assert.Equal(4m, fixture.Summary1.TotalCartons);
        Assert.Equal(80m, fixture.Summary1.TotalQuantity);
        Assert.Equal(8m, fixture.Summary1.TotalCbm);
        Assert.Equal(800m, fixture.Summary1.TotalGrossWeightKg);
        Assert.Equal(800m, fixture.Summary1.PurchaseAmount);
        Assert.Equal(1200m, fixture.Summary1.SalesAmount);
        Assert.Equal(400m, fixture.Summary1.ExpectedProfit);
    }

    [Fact]
    public async Task Reserve_RejectsCumulativeQuantityAbovePurchaseOrderCartons()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db, poCartons: 10m);
        var service = new SummaryReservationService(db, new AuditTrailService(db));
        await service.ReserveAsync(fixture.Summary1.Id, fixture.PoLine.Id, 6m, 7);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ReserveAsync(fixture.Summary2.Id, fixture.PoLine.Id, 5m, 8));

        Assert.Equal("SUMMARY_RESERVATION_CONFLICT", ex.Code);
        Assert.Single(await db.SummaryOrderItems.ToListAsync());
    }

    [Fact]
    public async Task Release_DraftReservationRestoresAvailability()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db, poCartons: 10m);
        var service = new SummaryReservationService(db, new AuditTrailService(db));
        var first = await service.ReserveAsync(fixture.Summary1.Id, fixture.PoLine.Id, 8m, 7);

        await service.ReleaseAsync(first.Id, "移出汇总单", 7);
        var second = await service.ReserveAsync(fixture.Summary2.Id, fixture.PoLine.Id, 10m, 8);
        await db.Entry(fixture.Summary1).ReloadAsync();
        await db.Entry(fixture.Summary2).ReloadAsync();

        Assert.Equal("released", first.ReservationStatus);
        Assert.Equal(10m, second.ReservedCartons);
        Assert.Equal(0m, fixture.Summary1.TotalCartons);
        Assert.Equal(10m, fixture.Summary2.TotalCartons);
    }

    [Fact]
    public async Task SameCustomer_CanUseMultipleDraftSummaryOrders()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db, poCartons: 10m);
        var service = new SummaryReservationService(db, new AuditTrailService(db));

        await service.ReserveAsync(fixture.Summary1.Id, fixture.PoLine.Id, 4m, 7);
        await service.ReserveAsync(fixture.Summary2.Id, fixture.PoLine.Id, 6m, 8);

        Assert.Equal(2, await db.SummaryOrderItems.CountAsync());
        Assert.Equal(4m, fixture.Summary1.TotalCartons);
        Assert.Equal(6m, fixture.Summary2.TotalCartons);
    }

    [Fact]
    public async Task Reserve_RejectsDifferentCustomerOrUnconfirmedPurchaseOrder()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db, poCartons: 10m);
        var service = new SummaryReservationService(db, new AuditTrailService(db));

        fixture.Summary1.CustomerId += 999;
        await db.SaveChangesAsync();
        var customerEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ReserveAsync(fixture.Summary1.Id, fixture.PoLine.Id, 1m, 7));
        Assert.Equal("SUMMARY_CUSTOMER_MISMATCH", customerEx.Code);

        fixture.Summary1.CustomerId = fixture.PurchaseOrder.CustomerId!.Value;
        fixture.PurchaseOrder.Status = "draft";
        await db.SaveChangesAsync();
        var statusEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ReserveAsync(fixture.Summary1.Id, fixture.PoLine.Id, 1m, 7));
        Assert.Equal("PURCHASE_ORDER_NOT_CONFIRMED", statusEx.Code);
    }

    [Fact]
    public async Task Confirm_LocksAllocationsAndPreventsRelease()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db, poCartons: 10m);
        var service = new SummaryReservationService(db, new AuditTrailService(db));
        var allocation = await service.ReserveAsync(fixture.Summary1.Id, fixture.PoLine.Id, 4m, 7);

        var summary = await service.ConfirmAsync(fixture.Summary1.Id, 7);

        Assert.Equal("confirmed", summary.Status);
        Assert.NotNull(summary.ConfirmedAt);
        Assert.Equal("confirmed", allocation.ReservationStatus);
        Assert.NotNull(allocation.ConfirmedAt);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ReleaseAsync(allocation.Id, "尝试释放", 7));
        Assert.Equal("SUMMARY_ALLOCATION_LOCKED", ex.Code);
    }

    private static async Task<SummaryFixture> CreateFixtureAsync(
        Futurem.Sourcing.Api.Data.AppDbContext db,
        decimal poCartons)
    {
        var customer = new Customer { Code = "C001", Name = "客户一", Currency = "RMB" };
        var supplier = new Supplier { Code = "S001", Name = "供应商一" };
        db.AddRange(customer, supplier);
        await db.SaveChangesAsync();

        var customerOrder = new CustomerOrder
        {
            No = "CO001",
            CustomerId = customer.Id,
            Status = "confirmed",
            Currency = "RMB"
        };
        db.CustomerOrders.Add(customerOrder);
        await db.SaveChangesAsync();

        var orderProduct = new OrderProduct
        {
            CustomerId = customer.Id,
            SupplierId = supplier.Id,
            SourceCustomerOrderId = customerOrder.Id,
            SystemSku = "SKU001",
            CustomerBarcode = "CUS001",
            NameCn = "测试商品",
            Unit = "PCS",
            PurchaseUnitPrice = 10m,
            SalesUnitPrice = 15m,
            CartonQty = 20m,
            CartonCbm = 2m,
            CartonGwKg = 200m,
            CartonNwKg = 180m,
            ImporterProfileId = 1,
            LabelTemplateId = 1,
            MarkTemplateId = 2,
            Status = "locked"
        };
        db.OrderProducts.Add(orderProduct);
        await db.SaveChangesAsync();

        var po = new PurchaseOrder
        {
            No = "PO001",
            CustomerOrderId = customerOrder.Id,
            CustomerId = customer.Id,
            SupplierId = supplier.Id,
            Status = "confirmed",
            Currency = "RMB"
        };
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();

        var poLine = new DocumentLine
        {
            DocumentType = "PO",
            DocumentId = po.Id,
            OrderProductId = orderProduct.Id,
            CustomerId = customer.Id,
            SupplierId = supplier.Id,
            Sku = orderProduct.SystemSku,
            ProductName = orderProduct.NameCn,
            Unit = "PCS",
            Quantity = poCartons * orderProduct.CartonQty,
            CartonQty = orderProduct.CartonQty,
            Cartons = poCartons,
            CartonCbm = orderProduct.CartonCbm,
            TotalCbm = poCartons * orderProduct.CartonCbm,
            CartonGwKg = orderProduct.CartonGwKg,
            TotalGwKg = poCartons * orderProduct.CartonGwKg,
            CartonNwKg = orderProduct.CartonNwKg,
            TotalNwKg = poCartons * orderProduct.CartonNwKg,
            PurchaseUnitPriceSnapshot = orderProduct.PurchaseUnitPrice,
            SalesUnitPriceSnapshot = orderProduct.SalesUnitPrice,
            UnitPrice = orderProduct.PurchaseUnitPrice,
            Amount = poCartons * orderProduct.CartonQty * orderProduct.PurchaseUnitPrice
        };
        db.DocumentLines.Add(poLine);

        var summary1 = NewSummary(customer.Id, "SUM001");
        var summary2 = NewSummary(customer.Id, "SUM002");
        db.SummaryOrders.AddRange(summary1, summary2);
        await db.SaveChangesAsync();

        return new SummaryFixture(customer, supplier, customerOrder, orderProduct, po, poLine, summary1, summary2);
    }

    private static SummaryOrder NewSummary(long customerId, string no) => new()
    {
        No = no,
        CustomerId = customerId,
        Currency = "RMB",
        Status = "draft",
        OrderDate = DateTime.Today
    };

    private sealed record SummaryFixture(
        Customer Customer,
        Supplier Supplier,
        CustomerOrder CustomerOrder,
        OrderProduct OrderProduct,
        PurchaseOrder PurchaseOrder,
        DocumentLine PoLine,
        SummaryOrder Summary1,
        SummaryOrder Summary2);
}
