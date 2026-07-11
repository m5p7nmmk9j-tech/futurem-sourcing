using Futurem.Sourcing.Api.Controllers;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;

namespace Futurem.Sourcing.Api.Tests.Services;

public class CustomerSummaryDeliveryNoticeWorkflowTests
{
    [Fact]
    public async Task ConfirmSummary_AutomaticallyGeneratesDeliveryNotices()
    {
        await using var db = TestDbFactory.Create();
        var customer = new Customer { Code = "C-AUTO", Name = "自动通知客户", Currency = "RMB" };
        var supplier = new Supplier { Code = "S-AUTO", Name = "自动通知供应商" };
        db.AddRange(customer, supplier);
        await db.SaveChangesAsync();

        var co = new CustomerOrder { No = "CO-AUTO", CustomerId = customer.Id, Status = "confirmed", Currency = "RMB" };
        db.CustomerOrders.Add(co);
        await db.SaveChangesAsync();

        var product = new OrderProduct
        {
            CustomerId = customer.Id,
            SupplierId = supplier.Id,
            SourceCustomerOrderId = co.Id,
            SystemSku = "AUTO-1",
            CustomerBarcode = "AUTO-BAR",
            NameCn = "自动通知商品",
            Unit = "PCS",
            PurchaseUnitPrice = 10m,
            SalesUnitPrice = 15m,
            CartonQty = 20m,
            ImporterProfileId = 1,
            LabelTemplateId = 1,
            MarkTemplateId = 2,
            Status = "locked"
        };
        db.OrderProducts.Add(product);
        await db.SaveChangesAsync();

        var po = new PurchaseOrder
        {
            No = "PO-AUTO",
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
            DocumentType = "PO",
            DocumentId = po.Id,
            OrderProductId = product.Id,
            CustomerId = customer.Id,
            SupplierId = supplier.Id,
            Sku = product.SystemSku,
            ProductName = product.NameCn,
            Unit = "PCS",
            Quantity = 100m,
            CartonQty = 20m,
            Cartons = 5m,
            PurchaseUnitPriceSnapshot = 10m,
            SalesUnitPriceSnapshot = 15m
        };
        db.DocumentLines.Add(poLine);

        var summary = new SummaryOrder
        {
            No = "SUM-AUTO",
            CustomerId = customer.Id,
            Currency = "RMB",
            Status = "draft",
            OrderDate = DateTime.Today,
            WarehouseId = 9,
            PlannedDeliveryDate = new DateTime(2026, 7, 20)
        };
        db.SummaryOrders.Add(summary);
        await db.SaveChangesAsync();

        db.SummaryOrderItems.Add(new SummaryOrderItem
        {
            SummaryOrderId = summary.Id,
            PurchaseOrderId = po.Id,
            PurchaseOrderLineId = poLine.Id,
            OrderProductId = product.Id,
            SupplierId = supplier.Id,
            ReservedCartons = 5m,
            ReservedQuantity = 100m,
            ReservationStatus = "draft_reserved"
        });
        await db.SaveChangesAsync();

        var audit = new AuditTrailService(db);
        var controller = new CustomerSummariesController(
            db,
            new SummaryReservationService(db, audit),
            new DeliveryNoticeService(db, audit));

        await controller.Confirm(summary.Id);

        var notice = Assert.Single(db.DeliveryNotices);
        Assert.Equal(summary.Id, notice.SummaryOrderId);
        Assert.Equal(supplier.Id, notice.SupplierId);
        Assert.Equal(9, notice.WarehouseId);
        Assert.Equal(new DateTime(2026, 7, 20), notice.PlannedDeliveryDate);
    }
}
