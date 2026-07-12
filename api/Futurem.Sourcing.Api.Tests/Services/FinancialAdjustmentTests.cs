using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Tests.Services;

public class FinancialAdjustmentTests
{
    [Fact]
    public async Task Apply_SupplierRefundAfterFullPayment_PreservesPaymentAndCreatesCredit()
    {
        await using var db = TestDbFactory.Create();
        var supplier = new Supplier { Code = "ADJ-S", Name = "调整供应商" };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var payable = new FinanceRecord
        {
            No = "AP-ADJ",
            RecordType = "payable",
            TargetType = "QC",
            TargetId = 1,
            SupplierId = supplier.Id,
            CounterpartyType = "product_supplier",
            Currency = "RMB",
            Amount = 1000m,
            PaidAmount = 1000m,
            Status = "done",
            SourceKey = "qc:adjustment:test",
            CreatedAt = DateTime.Now
        };
        db.FinanceRecords.Add(payable);
        await db.SaveChangesAsync();
        db.FinanceRecordLines.Add(new FinanceRecordLine
        {
            FinanceRecordId = payable.Id,
            SourceKey = "qc:adjustment:test:base",
            LineType = "goods",
            SourceType = "QC",
            Amount = 1000m,
            PaidAmount = 1000m,
            Status = "done",
            CreatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        var service = new FinancialAdjustmentService(db, new FinanceDocumentService(db), new AuditTrailService(db));
        var adjustment = await service.CreateAsync(new FinancialAdjustmentCreateInput(
            payable.Id,
            "supplier_refund_or_credit",
            -200m,
            "验货数量减少",
            "QC_ORDER",
            1), 7);
        await service.ApproveAsync(adjustment.Id, 8);
        var applied = await service.ApplyAsync(adjustment.Id, 9);

        Assert.Equal("applied", applied.Status);
        Assert.Equal(800m, payable.Amount);
        Assert.Equal(1000m, payable.PaidAmount);
        Assert.Equal(200m, payable.OverpaymentTransferredAmount);
        var credit = await db.SupplierPrepayments.SingleAsync();
        Assert.Equal("product_supplier", credit.CounterpartyType);
        Assert.Equal(supplier.Id, credit.SupplierId);
        Assert.Equal(200m, credit.AvailableAmount);
        Assert.Contains(await db.FinanceRecordLines.ToListAsync(), x =>
            x.SourceKey == $"adjustment:{adjustment.Id}" && x.Amount == -200m);
    }

    [Fact]
    public async Task Apply_IsIdempotentAndDoesNotDuplicateFinanceLineOrCredit()
    {
        await using var db = TestDbFactory.Create();
        var supplier = new Supplier { Code = "ADJ-I", Name = "幂等供应商" };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        var payable = new FinanceRecord
        {
            No = "AP-IDEM",
            RecordType = "payable",
            TargetType = "QC",
            TargetId = 2,
            SupplierId = supplier.Id,
            CounterpartyType = "product_supplier",
            Currency = "RMB",
            Amount = 500m,
            PaidAmount = 500m,
            Status = "done",
            CreatedAt = DateTime.Now
        };
        db.FinanceRecords.Add(payable);
        await db.SaveChangesAsync();
        db.FinanceRecordLines.Add(new FinanceRecordLine
        {
            FinanceRecordId = payable.Id,
            SourceKey = "ap:idem:base",
            LineType = "goods",
            SourceType = "QC",
            Amount = 500m,
            PaidAmount = 500m,
            Status = "done",
            CreatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        var service = new FinancialAdjustmentService(db, new FinanceDocumentService(db), new AuditTrailService(db));
        var adjustment = await service.CreateAsync(new FinancialAdjustmentCreateInput(
            payable.Id,
            "supplier_refund_or_credit",
            -100m,
            "重复应用测试",
            "QC_ORDER",
            2), 1);
        await service.ApproveAsync(adjustment.Id, 2);
        await service.ApplyAsync(adjustment.Id, 3);
        await service.ApplyAsync(adjustment.Id, 3);

        Assert.Equal(1, await db.FinanceRecordLines.CountAsync(x => x.SourceKey == $"adjustment:{adjustment.Id}"));
        Assert.Equal(1, await db.SupplierPrepayments.CountAsync());
        Assert.Equal(400m, payable.Amount);
    }

    [Fact]
    public async Task Apply_RequiresApprovedStatus()
    {
        await using var db = TestDbFactory.Create();
        var record = new FinanceRecord
        {
            No = "AR-DRAFT",
            RecordType = "receivable",
            TargetType = "CONTAINER",
            TargetId = 3,
            CustomerId = 1,
            CounterpartyType = "customer",
            Currency = "RMB",
            Amount = 100m,
            Status = "pending",
            CreatedAt = DateTime.Now
        };
        db.FinanceRecords.Add(record);
        await db.SaveChangesAsync();
        var service = new FinancialAdjustmentService(db, new FinanceDocumentService(db), new AuditTrailService(db));
        var adjustment = await service.CreateAsync(new FinancialAdjustmentCreateInput(
            record.Id,
            "customer_receivable_adjustment",
            20m,
            "补收费用",
            "CONTAINER_LOAD",
            3), 1);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ApplyAsync(adjustment.Id, 2));
        Assert.Equal("FINANCIAL_ADJUSTMENT_NOT_APPROVED", ex.Code);
    }
}
