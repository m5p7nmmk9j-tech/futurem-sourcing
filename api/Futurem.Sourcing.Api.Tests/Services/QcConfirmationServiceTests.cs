using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Tests.Services;

public class QcConfirmationServiceTests
{
    [Fact]
    public async Task CreateDraft_RejectsSecondActiveQcForReceiving()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = new QcConfirmationService(db, new AuditTrailService(db));

        var first = await service.CreateDraftAsync(fixture.Receiving.Id, 7);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateDraftAsync(fixture.Receiving.Id, 8));

        Assert.True(first.Id > 0);
        Assert.Equal("RECEIVING_ALREADY_HAS_QC", ex.Code);
        Assert.Single(await db.QcOrders.ToListAsync());
    }

    [Fact]
    public async Task Confirm_RejectsBrokenQuantityEquation()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = new QcConfirmationService(db, new AuditTrailService(db));
        var qc = await service.CreateDraftAsync(fixture.Receiving.Id, 7);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ConfirmAsync(qc.Id,
            [
                new QcLineResult(
                    fixture.ReceivingLine.Id,
                    ArrivedQuantity: 100,
                    QualifiedQuantity: 80,
                    UnqualifiedQuantity: 10,
                    ReturnedQuantity: 5,
                    PendingQuantity: 4,
                    AcceptedQuantity: 80)
            ], 7));

        Assert.Equal("QC_QUANTITY_EQUATION_INVALID", ex.Code);
        Assert.Empty(await db.QcOrderLines.ToListAsync());
        Assert.Empty(await db.FinanceRecords.ToListAsync());
    }

    [Fact]
    public async Task Confirm_RejectsAcceptedQuantityAboveArrived()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = new QcConfirmationService(db, new AuditTrailService(db));
        var qc = await service.CreateDraftAsync(fixture.Receiving.Id, 7);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ConfirmAsync(qc.Id,
            [
                new QcLineResult(
                    fixture.ReceivingLine.Id,
                    ArrivedQuantity: 100,
                    QualifiedQuantity: 100,
                    UnqualifiedQuantity: 0,
                    ReturnedQuantity: 0,
                    PendingQuantity: 0,
                    AcceptedQuantity: 101)
            ], 7));

        Assert.Equal("QC_ACCEPTED_OVER_ARRIVED", ex.Code);
    }

    [Fact]
    public async Task Confirm_CreatesPayableFromFinalAcceptedQuantityAndIsIdempotent()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = new QcConfirmationService(db, new AuditTrailService(db));
        var qc = await service.CreateDraftAsync(fixture.Receiving.Id, 7);
        var results = new[]
        {
            new QcLineResult(
                fixture.ReceivingLine.Id,
                ArrivedQuantity: 100,
                QualifiedQuantity: 82,
                UnqualifiedQuantity: 8,
                ReturnedQuantity: 5,
                PendingQuantity: 5,
                AcceptedQuantity: 82)
        };

        var first = await service.ConfirmAsync(qc.Id, results, 7);
        var second = await service.ConfirmAsync(qc.Id, results, 7);

        Assert.Equal("confirmed", first.Status);
        Assert.Equal(first.ConfirmedAt, second.ConfirmedAt);
        var qcLine = Assert.Single(await db.QcOrderLines.ToListAsync());
        Assert.Equal(82m, qcLine.AcceptedQuantity);
        Assert.Equal(8m, qcLine.UnqualifiedQuantity);
        Assert.Equal(5m, qcLine.ReturnedQuantity);
        Assert.Equal(5m, qcLine.PendingQuantity);

        var payable = Assert.Single(await db.FinanceRecords.ToListAsync());
        Assert.Equal("payable", payable.RecordType);
        Assert.Equal("QC_ACCEPTED", payable.TargetType);
        Assert.Equal(fixture.Supplier.Id, payable.SupplierId);
        Assert.Equal(820m, payable.Amount);
        Assert.Equal($"qc:{qc.Id}:line:{qcLine.Id}", payable.SourceKey);
        Assert.Equal("pending", payable.Status);
        Assert.Equal("qc_confirmed", fixture.Receiving.Status);
    }

    [Fact]
    public async Task Unlock_RequiresReasonAndUnpaidReconfirmUpdatesPayable()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = new QcConfirmationService(db, new AuditTrailService(db));
        var qc = await service.CreateDraftAsync(fixture.Receiving.Id, 7);
        await service.ConfirmAsync(qc.Id,
        [
            new QcLineResult(fixture.ReceivingLine.Id, 100, 82, 8, 5, 5, 82)
        ], 7);

        var reasonEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UnlockAsync(qc.Id, "", 9));
        Assert.Equal("QC_UNLOCK_REASON_REQUIRED", reasonEx.Code);

        await service.UnlockAsync(qc.Id, "供应商返工后重新点数", 9);
        await service.ConfirmAsync(qc.Id,
        [
            new QcLineResult(fixture.ReceivingLine.Id, 100, 70, 10, 10, 10, 70)
        ], 9);

        var payable = Assert.Single(await db.FinanceRecords.ToListAsync());
        Assert.Equal(700m, payable.Amount);
        Assert.Equal("pending", payable.Status);
        Assert.Equal(2, qc.ConfirmationVersion);
        Assert.Equal("confirmed", qc.Status);
    }

    [Fact]
    public async Task ReconfirmPaidDecreaseCreatesCreditAdjustmentWithoutRewritingPaidPayable()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = new QcConfirmationService(db, new AuditTrailService(db));
        var qc = await service.CreateDraftAsync(fixture.Receiving.Id, 7);
        await service.ConfirmAsync(qc.Id,
        [
            new QcLineResult(fixture.ReceivingLine.Id, 100, 82, 8, 5, 5, 82)
        ], 7);
        var payable = await db.FinanceRecords.SingleAsync();
        payable.PaidAmount = 820m;
        FinanceBalanceService.RefreshStatus(payable);
        await db.SaveChangesAsync();

        await service.UnlockAsync(qc.Id, "付款后复核数量", 9);
        await service.ConfirmAsync(qc.Id,
        [
            new QcLineResult(fixture.ReceivingLine.Id, 100, 70, 10, 10, 10, 70)
        ], 9);

        Assert.Equal(820m, payable.Amount);
        Assert.Equal(820m, payable.PaidAmount);
        Assert.Equal("done", payable.Status);
        var adjustment = Assert.Single(await db.FinancialAdjustments.ToListAsync());
        Assert.Equal("supplier_refund_or_credit", adjustment.AdjustmentType);
        Assert.Equal(120m, adjustment.Amount);
        Assert.Equal(payable.Id, adjustment.FinanceRecordId);
        Assert.Equal($"qc:{qc.Id}:line:{adjustment.QcOrderLineId}:version:2:credit", adjustment.SourceKey);
    }

    private static async Task<Fixture> CreateFixtureAsync(AppDbContext db)
    {
        var customer = new Customer { Code = "C-QC", Name = "验货客户", Currency = "RMB" };
        var supplier = new Supplier { Code = "S-QC", Name = "验货供应商" };
        db.AddRange(customer, supplier);
        await db.SaveChangesAsync();

        var co = new CustomerOrder
        {
            No = "CO-QC",
            CustomerId = customer.Id,
            Status = "confirmed",
            Currency = "RMB"
        };
        db.CustomerOrders.Add(co);
        await db.SaveChangesAsync();

        var product = new OrderProduct
        {
            CustomerId = customer.Id,
            SupplierId = supplier.Id,
            SourceCustomerOrderId = co.Id,
            SystemSku = "SKU-QC",
            CustomerBarcode = "BAR-QC",
            NameCn = "验货商品",
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
            No = "PO-QC",
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
            SalesUnitPriceSnapshot = 15m,
            UnitPrice = 10m,
            Amount = 1000m
        };
        db.DocumentLines.Add(poLine);
        await db.SaveChangesAsync();

        var receiving = new ReceivingOrder
        {
            No = "RCV-QC",
            PurchaseOrderId = po.Id,
            WarehouseId = 3,
            SupplierId = supplier.Id,
            ReceiveDate = DateTime.Today,
            Status = "received",
            TemporaryQuantity = 100m,
            TemporaryCartons = 5m
        };
        db.ReceivingOrders.Add(receiving);
        await db.SaveChangesAsync();

        var receivingLine = new DocumentLine
        {
            DocumentType = "RCV",
            DocumentId = receiving.Id,
            OrderProductId = product.Id,
            SourceDocumentLineId = poLine.Id,
            CustomerId = customer.Id,
            SupplierId = supplier.Id,
            WarehouseId = 3,
            Sku = product.SystemSku,
            ProductName = product.NameCn,
            Unit = "PCS",
            Quantity = 100m,
            CartonQty = 20m,
            Cartons = 5m,
            PurchaseUnitPriceSnapshot = 10m,
            SalesUnitPriceSnapshot = 15m,
            UnitPrice = 0m,
            Amount = 0m
        };
        db.DocumentLines.Add(receivingLine);
        await db.SaveChangesAsync();

        return new Fixture(customer, supplier, product, po, poLine, receiving, receivingLine);
    }

    private sealed record Fixture(
        Customer Customer,
        Supplier Supplier,
        OrderProduct Product,
        PurchaseOrder PurchaseOrder,
        DocumentLine PurchaseOrderLine,
        ReceivingOrder Receiving,
        DocumentLine ReceivingLine);
}
