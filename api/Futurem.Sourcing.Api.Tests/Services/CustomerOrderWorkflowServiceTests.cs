using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Tests.Services;

public class CustomerOrderWorkflowServiceTests
{
    [Fact]
    public async Task CopyToOrder_CreatesIndependentProductImagesAndLine()
    {
        await using var db = TestDbFactory.Create();
        var customer = new Customer { Code = "C001", Name = "客户一" };
        var supplier = new Supplier { Code = "S001", Name = "供应商一" };
        db.AddRange(customer, supplier);
        await db.SaveChangesAsync();

        var sourceOrder = NewOrder(customer.Id);
        var targetOrder = NewOrder(customer.Id);
        db.AddRange(sourceOrder, targetOrder);
        await db.SaveChangesAsync();

        var source = NewProduct(sourceOrder.Id, customer.Id, supplier.Id, "CUS-001");
        db.OrderProducts.Add(source);
        await db.SaveChangesAsync();
        db.OrderProductImages.Add(new OrderProductImage
        {
            OrderProductId = source.Id,
            ImageType = "main",
            ImageUrl = "https://example.test/main.jpg"
        });
        db.DocumentLines.Add(NewCoLine(sourceOrder.Id, source.Id, 100m, 10m));
        await db.SaveChangesAsync();

        var service = new OrderProductService(db);
        var copied = await service.CopyToOrderAsync(source.Id, targetOrder.Id, supplier.Id);

        Assert.NotEqual(source.Id, copied.Id);
        Assert.Equal(source.Id, copied.SourceOrderProductId);
        Assert.Equal(targetOrder.Id, copied.SourceCustomerOrderId);
        Assert.Equal(source.CustomerBarcode, copied.CustomerBarcode);
        Assert.Equal("draft", copied.Status);
        Assert.Null(copied.LockedAt);
        Assert.Equal("locked", source.Status);
        Assert.Single(await db.OrderProductImages.Where(x => x.OrderProductId == copied.Id).ToListAsync());
        var copiedLine = await db.DocumentLines.SingleAsync(x => x.DocumentType == "CO" && x.DocumentId == targetOrder.Id);
        Assert.Equal(copied.Id, copiedLine.OrderProductId);
        Assert.Equal(100m, copiedLine.Quantity);
        Assert.Equal(10m, copiedLine.Cartons);
    }

    [Fact]
    public async Task Confirm_LocksOneImporterAndTemplateSetAcrossAllProducts()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateConfirmedFixturePrerequisites(db, productCount: 2);
        var service = new CustomerOrderWorkflowService(db, new AuditTrailService(db));

        var confirmed = await service.ConfirmAsync(fixture.Order.Id, "客户确认", 7);

        Assert.Equal("confirmed", confirmed.Status);
        Assert.NotNull(confirmed.ConfirmedAt);
        Assert.Contains("IMPORTADORA FUTUREM", confirmed.ImporterSnapshotJson);
        var products = await db.OrderProducts
            .Where(x => x.SourceCustomerOrderId == confirmed.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();
        Assert.Equal(2, products.Count);
        Assert.All(products, product =>
        {
            Assert.Equal(confirmed.ImporterProfileId, product.ImporterProfileId);
            Assert.Equal(confirmed.LabelTemplateId, product.LabelTemplateId);
            Assert.Equal(confirmed.MarkTemplateId, product.MarkTemplateId);
            Assert.Equal(confirmed.ImporterSnapshotJson, product.ImporterSnapshotJson);
            Assert.Equal(confirmed.LabelTemplateSnapshotJson, product.LabelTemplateSnapshotJson);
            Assert.Equal(confirmed.MarkTemplateSnapshotJson, product.MarkTemplateSnapshotJson);
            Assert.Equal("locked", product.Status);
            Assert.NotNull(product.LockedAt);
        });
        Assert.Contains(await db.AuditLogs.ToListAsync(), x => x.Action == "confirm" && x.TargetId == confirmed.Id);
    }

    [Fact]
    public async Task GeneratePo_RejectsPartialOrderProductQuantity()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateConfirmedFixturePrerequisites(db, productCount: 1);
        var service = new CustomerOrderWorkflowService(db, new AuditTrailService(db));
        await service.ConfirmAsync(fixture.Order.Id, "客户确认", 7);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.GeneratePoAsync(
                fixture.Order.Id,
                new[] { new GeneratePoItem(fixture.Products[0].Id, 60m) },
                fixture.Supplier.Id,
                null,
                7));

        Assert.Equal("ORDER_PRODUCT_MUST_CONVERT_FULL_QUANTITY", ex.Code);
        Assert.Empty(db.PurchaseOrders);
    }

    [Fact]
    public async Task GeneratePo_CopiesOnlySelectedFullProductsAndUpdatesConversionStatus()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateConfirmedFixturePrerequisites(db, productCount: 2);
        var service = new CustomerOrderWorkflowService(db, new AuditTrailService(db));
        await service.ConfirmAsync(fixture.Order.Id, "客户确认", 7);

        var firstPo = await service.GeneratePoAsync(
            fixture.Order.Id,
            new[] { new GeneratePoItem(fixture.Products[0].Id, 100m) },
            fixture.Supplier.Id,
            DateTime.Today.AddDays(30),
            7);

        Assert.Equal("RMB", firstPo.Currency);
        Assert.Equal(fixture.Order.ImporterProfileId, firstPo.ImporterProfileId);
        Assert.Equal("partially_converted", fixture.Order.Status);
        var firstLine = await db.DocumentLines.SingleAsync(x => x.DocumentType == "PO" && x.DocumentId == firstPo.Id);
        Assert.Equal(fixture.Products[0].Id, firstLine.OrderProductId);
        Assert.Equal(100m, firstLine.Quantity);
        Assert.Equal(fixture.Products[0].PurchaseUnitPrice, firstLine.PurchaseUnitPriceSnapshot);
        Assert.Equal(fixture.Products[0].SalesUnitPrice, firstLine.SalesUnitPriceSnapshot);

        await service.GeneratePoAsync(
            fixture.Order.Id,
            new[] { new GeneratePoItem(fixture.Products[1].Id, 100m) },
            fixture.Supplier.Id,
            null,
            7);

        Assert.Equal("converted", fixture.Order.Status);
        Assert.Equal(2, await db.PurchaseOrders.CountAsync());
    }

    [Fact]
    public async Task GeneratePo_RejectsProductAlreadyInActivePo()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateConfirmedFixturePrerequisites(db, productCount: 1);
        var service = new CustomerOrderWorkflowService(db, new AuditTrailService(db));
        await service.ConfirmAsync(fixture.Order.Id, "客户确认", 7);
        await service.GeneratePoAsync(
            fixture.Order.Id,
            new[] { new GeneratePoItem(fixture.Products[0].Id, 100m) },
            fixture.Supplier.Id,
            null,
            7);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.GeneratePoAsync(
                fixture.Order.Id,
                new[] { new GeneratePoItem(fixture.Products[0].Id, 100m) },
                fixture.Supplier.Id,
                null,
                7));

        Assert.Equal("ORDER_PRODUCT_ALREADY_PURCHASED", ex.Code);
    }

    private static CustomerOrder NewOrder(long customerId) => new()
    {
        No = Guid.NewGuid().ToString("N"),
        CustomerId = customerId,
        OrderDate = DateTime.Today,
        Currency = "RMB",
        Status = "draft"
    };

    private static OrderProduct NewProduct(
        long customerOrderId,
        long customerId,
        long supplierId,
        string barcode) => new()
    {
        SourceCustomerOrderId = customerOrderId,
        CustomerId = customerId,
        SupplierId = supplierId,
        SystemSku = $"SKU-{barcode}",
        CustomerItemNo = barcode,
        CustomerBarcode = barcode,
        NameCn = "测试商品",
        Unit = "PCS",
        PurchaseUnitPrice = 10m,
        SalesUnitPrice = 15m,
        CartonQty = 10m,
        CartonLengthCm = 50m,
        CartonWidthCm = 40m,
        CartonHeightCm = 30m,
        CartonCbm = 0.06m,
        CartonGwKg = 12m,
        CartonNwKg = 10m,
        ImporterProfileId = 1,
        LabelTemplateId = 1,
        MarkTemplateId = 2,
        ImporterSnapshotJson = "{}",
        LabelTemplateSnapshotJson = "{}",
        MarkTemplateSnapshotJson = "{}",
        BatchCode = "20260711",
        Status = "locked",
        LockedAt = DateTime.Now
    };

    private static DocumentLine NewCoLine(long orderId, long orderProductId, decimal quantity, decimal cartons) => new()
    {
        DocumentType = "CO",
        DocumentId = orderId,
        OrderProductId = orderProductId,
        Sku = $"SKU-{orderProductId}",
        ProductName = "测试商品",
        Unit = "PCS",
        Quantity = quantity,
        CartonQty = quantity / cartons,
        Cartons = cartons,
        UnitPrice = 15m,
        Amount = quantity * 15m,
        PurchaseUnitPriceSnapshot = 10m,
        SalesUnitPriceSnapshot = 15m,
        CreatedAt = DateTime.Now
    };

    private static async Task<WorkflowFixture> CreateConfirmedFixturePrerequisites(
        Futurem.Sourcing.Api.Data.AppDbContext db,
        int productCount)
    {
        var customer = new Customer { Code = "C001", Name = "客户一" };
        var supplier = new Supplier { Code = "S001", Name = "供应商一" };
        db.AddRange(customer, supplier);
        await db.SaveChangesAsync();

        var importer = new CustomerImporterProfile
        {
            CustomerId = customer.Id,
            Name = "墨西哥进口商",
            CompanyName = "IMPORTADORA FUTUREM",
            TaxIdOrRfc = "FUT260711AA1",
            Address = "Ciudad de México",
            IsDefault = true
        };
        var label = new PrintTemplate
        {
            Code = "LBL-1",
            Name = "标签一",
            DocumentType = "CO",
            TemplateType = "product_label",
            CustomerId = customer.Id,
            Body = "{{CustomerBarcode}}",
            LayoutJson = "{}"
        };
        var mark = new PrintTemplate
        {
            Code = "MARK-1",
            Name = "唛头一",
            DocumentType = "CO",
            TemplateType = "carton_mark",
            CustomerId = customer.Id,
            Body = "{{CustomerItemNo}}",
            LayoutJson = "{}"
        };
        db.AddRange(importer, label, mark);
        await db.SaveChangesAsync();

        var order = NewOrder(customer.Id);
        order.ImporterProfileId = importer.Id;
        order.LabelTemplateId = label.Id;
        order.MarkTemplateId = mark.Id;
        db.CustomerOrders.Add(order);
        await db.SaveChangesAsync();

        var products = new List<OrderProduct>();
        for (var i = 1; i <= productCount; i++)
        {
            var product = NewProduct(order.Id, customer.Id, supplier.Id, $"CUS-{i:000}");
            product.Status = "draft";
            product.LockedAt = null;
            product.ImporterProfileId = 0;
            product.LabelTemplateId = 0;
            product.MarkTemplateId = 0;
            products.Add(product);
            db.OrderProducts.Add(product);
            await db.SaveChangesAsync();
            db.DocumentLines.Add(NewCoLine(order.Id, product.Id, 100m, 10m));
        }
        await db.SaveChangesAsync();

        return new WorkflowFixture(customer, supplier, importer, label, mark, order, products);
    }

    private sealed record WorkflowFixture(
        Customer Customer,
        Supplier Supplier,
        CustomerImporterProfile Importer,
        PrintTemplate Label,
        PrintTemplate Mark,
        CustomerOrder Order,
        List<OrderProduct> Products);
}
