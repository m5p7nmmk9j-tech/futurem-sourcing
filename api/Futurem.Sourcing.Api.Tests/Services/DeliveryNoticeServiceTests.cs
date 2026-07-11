using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Tests.Services;

public class DeliveryNoticeServiceTests
{
    [Fact]
    public async Task GenerateForConfirmedSummary_GroupsBySupplierAndIsIdempotent()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = new DeliveryNoticeService(db, new AuditTrailService(db));
        var plannedDate = new DateTime(2026, 7, 20);

        var first = await service.GenerateForConfirmedSummaryAsync(
            fixture.Summary.Id,
            plannedDate,
            9,
            7);
        var second = await service.GenerateForConfirmedSummaryAsync(
            fixture.Summary.Id,
            plannedDate,
            9,
            7);

        Assert.Equal(2, first.Count);
        Assert.Equal(first.Select(x => x.Id).OrderBy(x => x), second.Select(x => x.Id).OrderBy(x => x));
        Assert.Equal(2, await db.DeliveryNotices.CountAsync());
        Assert.Equal(3, await db.DeliveryNoticeLines.CountAsync());

        var supplierOne = await db.DeliveryNotices.SingleAsync(x => x.SupplierId == fixture.Supplier1.Id);
        Assert.Equal(
            $"summary:{fixture.Summary.Id}:supplier:{fixture.Supplier1.Id}:warehouse:9:date:20260720",
            supplierOne.SourceKey);
        Assert.Equal(2, await db.DeliveryNoticeLines.CountAsync(x => x.DeliveryNoticeId == supplierOne.Id));
        Assert.Equal(8m, supplierOne.TotalCartons);
        Assert.Equal(160m, supplierOne.TotalQuantity);
    }

    [Fact]
    public async Task GenerateForConfirmedSummary_RejectsUnconfirmedSummary()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        fixture.Summary.Status = "draft";
        await db.SaveChangesAsync();
        var service = new DeliveryNoticeService(db, new AuditTrailService(db));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.GenerateForConfirmedSummaryAsync(fixture.Summary.Id, DateTime.Today, 1, 7));

        Assert.Equal("SUMMARY_NOT_CONFIRMED", ex.Code);
        Assert.Empty(db.DeliveryNotices);
    }

    [Fact]
    public async Task Publish_IsIdempotent()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = new DeliveryNoticeService(db, new AuditTrailService(db));
        var notice = (await service.GenerateForConfirmedSummaryAsync(
            fixture.Summary.Id,
            DateTime.Today,
            5,
            7)).First();

        var first = await service.PublishAsync(notice.Id, 7);
        var publishedAt = first.PublishedAt;
        var second = await service.PublishAsync(notice.Id, 7);

        Assert.Equal("published", second.Status);
        Assert.NotNull(publishedAt);
        Assert.Equal(publishedAt, second.PublishedAt);
    }

    [Fact]
    public async Task CreateReceiving_AllowsPartialAndRejectsCumulativeOverDeliveryNotice()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = new DeliveryNoticeService(db, new AuditTrailService(db));
        var notice = (await service.GenerateForConfirmedSummaryAsync(
                fixture.Summary.Id,
                DateTime.Today,
                5,
                7))
            .Single(x => x.SupplierId == fixture.Supplier2.Id);
        var noticeLine = await db.DeliveryNoticeLines.SingleAsync(x => x.DeliveryNoticeId == notice.Id);

        var receiving = await service.CreateReceivingAsync(
            notice.Id,
            [new ReceivingLineInput(noticeLine.Id, 40m, 2m, "首批")],
            7);

        Assert.Equal(notice.Id, receiving.DeliveryNoticeId);
        Assert.Equal(5, receiving.WarehouseId);
        Assert.Equal("received", receiving.Status);
        Assert.Equal("partially_received", notice.Status);
        Assert.Equal(40m, notice.ReceivedQuantity);
        Assert.Equal(2m, notice.ReceivedCartons);
        Assert.Single(await db.DocumentLines.Where(x => x.DocumentType == "RCV" && x.DocumentId == receiving.Id).ToListAsync());

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateReceivingAsync(
                notice.Id,
                [new ReceivingLineInput(noticeLine.Id, 80m, 4m, "超量")],
                8));

        Assert.Equal("DELIVERY_NOTICE_OVER_PLANNED", ex.Code);
        Assert.Single(await db.ReceivingOrders.ToListAsync());
    }

    [Fact]
    public async Task CreateReceiving_ClosesNoticeWhenAllPlannedGoodsArrive()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = new DeliveryNoticeService(db, new AuditTrailService(db));
        var notice = (await service.GenerateForConfirmedSummaryAsync(
                fixture.Summary.Id,
                DateTime.Today,
                5,
                7))
            .Single(x => x.SupplierId == fixture.Supplier2.Id);
        var noticeLine = await db.DeliveryNoticeLines.SingleAsync(x => x.DeliveryNoticeId == notice.Id);

        await service.CreateReceivingAsync(
            notice.Id,
            [new ReceivingLineInput(noticeLine.Id, noticeLine.PlannedQuantity, noticeLine.PlannedCartons, null)],
            7);

        Assert.Equal("received", notice.Status);
        Assert.Equal(notice.TotalQuantity, notice.ReceivedQuantity);
        Assert.Equal(notice.TotalCartons, notice.ReceivedCartons);
    }

    private static async Task<DeliveryFixture> CreateFixtureAsync(Futurem.Sourcing.Api.Data.AppDbContext db)
    {
        var customer = new Customer { Code = "C-DN", Name = "送货客户", Currency = "RMB" };
        var supplier1 = new Supplier { Code = "S-DN-1", Name = "送货供应商一" };
        var supplier2 = new Supplier { Code = "S-DN-2", Name = "送货供应商二" };
        db.AddRange(customer, supplier1, supplier2);
        await db.SaveChangesAsync();

        var co = new CustomerOrder
        {
            No = "CO-DN",
            CustomerId = customer.Id,
            Status = "confirmed",
            Currency = "RMB"
        };
        db.CustomerOrders.Add(co);
        await db.SaveChangesAsync();

        var product1 = NewProduct(customer.Id, supplier1.Id, co.Id, "DN-1");
        var product2 = NewProduct(customer.Id, supplier1.Id, co.Id, "DN-2");
        var product3 = NewProduct(customer.Id, supplier2.Id, co.Id, "DN-3");
        db.OrderProducts.AddRange(product1, product2, product3);
        await db.SaveChangesAsync();

        var po1 = NewPurchaseOrder(customer.Id, supplier1.Id, co.Id, "PO-DN-1");
        var po2 = NewPurchaseOrder(customer.Id, supplier2.Id, co.Id, "PO-DN-2");
        db.PurchaseOrders.AddRange(po1, po2);
        await db.SaveChangesAsync();

        var line1 = NewPoLine(po1.Id, customer.Id, supplier1.Id, product1, 5m);
        var line2 = NewPoLine(po1.Id, customer.Id, supplier1.Id, product2, 3m);
        var line3 = NewPoLine(po2.Id, customer.Id, supplier2.Id, product3, 5m);
        db.DocumentLines.AddRange(line1, line2, line3);

        var summary = new SummaryOrder
        {
            No = "SUM-DN",
            CustomerId = customer.Id,
            Currency = "RMB",
            Status = "confirmed",
            OrderDate = DateTime.Today,
            ConfirmedAt = DateTime.Now
        };
        db.SummaryOrders.Add(summary);
        await db.SaveChangesAsync();

        db.SummaryOrderItems.AddRange(
            NewSummaryItem(summary.Id, po1.Id, line1.Id, product1.Id, supplier1.Id, 5m),
            NewSummaryItem(summary.Id, po1.Id, line2.Id, product2.Id, supplier1.Id, 3m),
            NewSummaryItem(summary.Id, po2.Id, line3.Id, product3.Id, supplier2.Id, 5m));
        await db.SaveChangesAsync();

        return new DeliveryFixture(summary, supplier1, supplier2);
    }

    private static OrderProduct NewProduct(long customerId, long supplierId, long coId, string code) => new()
    {
        CustomerId = customerId,
        SupplierId = supplierId,
        SourceCustomerOrderId = coId,
        SystemSku = code,
        CustomerBarcode = $"BAR-{code}",
        NameCn = $"商品 {code}",
        Unit = "PCS",
        PurchaseUnitPrice = 10m,
        SalesUnitPrice = 15m,
        CartonQty = 20m,
        ImporterProfileId = 1,
        LabelTemplateId = 1,
        MarkTemplateId = 2,
        Status = "locked"
    };

    private static PurchaseOrder NewPurchaseOrder(long customerId, long supplierId, long coId, string no) => new()
    {
        No = no,
        CustomerOrderId = coId,
        CustomerId = customerId,
        SupplierId = supplierId,
        Status = "confirmed",
        Currency = "RMB"
    };

    private static DocumentLine NewPoLine(
        long poId,
        long customerId,
        long supplierId,
        OrderProduct product,
        decimal cartons) => new()
    {
        DocumentType = "PO",
        DocumentId = poId,
        OrderProductId = product.Id,
        CustomerId = customerId,
        SupplierId = supplierId,
        Sku = product.SystemSku,
        ProductName = product.NameCn,
        Unit = "PCS",
        CartonQty = product.CartonQty,
        Cartons = cartons,
        Quantity = cartons * product.CartonQty,
        PurchaseUnitPriceSnapshot = product.PurchaseUnitPrice,
        SalesUnitPriceSnapshot = product.SalesUnitPrice,
        UnitPrice = product.PurchaseUnitPrice,
        Amount = cartons * product.CartonQty * product.PurchaseUnitPrice
    };

    private static SummaryOrderItem NewSummaryItem(
        long summaryId,
        long poId,
        long poLineId,
        long productId,
        long supplierId,
        decimal cartons) => new()
    {
        SummaryOrderId = summaryId,
        PurchaseOrderId = poId,
        PurchaseOrderLineId = poLineId,
        OrderProductId = productId,
        SupplierId = supplierId,
        ReservedCartons = cartons,
        ReservedQuantity = cartons * 20m,
        ReservationStatus = "confirmed",
        ConfirmedAt = DateTime.Now
    };

    private sealed record DeliveryFixture(
        SummaryOrder Summary,
        Supplier Supplier1,
        Supplier Supplier2);
}
