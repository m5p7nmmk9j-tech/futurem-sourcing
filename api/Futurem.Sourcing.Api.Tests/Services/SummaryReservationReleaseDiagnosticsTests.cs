using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Tests.Services;

public class SummaryReservationReleaseDiagnosticsTests
{
    [Fact]
    public async Task Release_DiagnosticSnapshot()
    {
        await using var db = TestDbFactory.Create();
        var customer = new Customer { Code = "DC", Name = "诊断客户", Currency = "RMB" };
        var supplier = new Supplier { Code = "DS", Name = "诊断供应商" };
        db.AddRange(customer, supplier);
        await db.SaveChangesAsync();

        var co = new CustomerOrder { No = "DCO", CustomerId = customer.Id, Status = "confirmed", Currency = "RMB" };
        db.CustomerOrders.Add(co);
        await db.SaveChangesAsync();

        var product = new OrderProduct
        {
            CustomerId = customer.Id,
            SupplierId = supplier.Id,
            SourceCustomerOrderId = co.Id,
            SystemSku = "DSKU",
            CustomerBarcode = "DBAR",
            NameCn = "诊断商品",
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
            No = "DPO",
            CustomerOrderId = co.Id,
            CustomerId = customer.Id,
            SupplierId = supplier.Id,
            Status = "confirmed",
            Currency = "RMB"
        };
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync();

        var line = new DocumentLine
        {
            DocumentType = "PO",
            DocumentId = po.Id,
            OrderProductId = product.Id,
            CustomerId = customer.Id,
            SupplierId = supplier.Id,
            Sku = product.SystemSku,
            ProductName = product.NameCn,
            Unit = "PCS",
            Quantity = 200m,
            CartonQty = 20m,
            Cartons = 10m,
            PurchaseUnitPriceSnapshot = 10m,
            SalesUnitPriceSnapshot = 15m
        };
        var summary = new SummaryOrder
        {
            No = "DSUM",
            CustomerId = customer.Id,
            Currency = "RMB",
            Status = "draft",
            OrderDate = DateTime.Today
        };
        db.AddRange(line, summary);
        await db.SaveChangesAsync();

        var service = new SummaryReservationService(db, new AuditTrailService(db));
        var allocation = await service.ReserveAsync(summary.Id, line.Id, 8m, 1);
        await service.ReleaseAsync(allocation.Id, "诊断", 1);

        var storedItem = await db.SummaryOrderItems.AsNoTracking().SingleAsync(x => x.Id == allocation.Id);
        var storedSummary = await db.SummaryOrders.AsNoTracking().SingleAsync(x => x.Id == summary.Id);
        var trackedSummary = db.Entry(summary);
        var localItems = string.Join(",", db.SummaryOrderItems.Local.Select(x => $"{x.Id}:{x.ReservationStatus}:{x.SummaryOrderId}"));

        Assert.True(false,
            $"storedItem={storedItem.Id}:{storedItem.ReservationStatus}; " +
            $"excluded={allocation.Id}; storedTotal={storedSummary.TotalCartons}; " +
            $"trackedTotal={summary.TotalCartons}; summaryState={trackedSummary.State}; localItems={localItems}");
    }
}
