using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Tests.Services;

public class FifoSettlementServiceTests
{
    [Fact]
    public async Task CustomerReceipt_AppliesOldestLinesFirstAndCreatesAdvance()
    {
        await using var db = TestDbFactory.Create();
        var customer = new Customer { Code = "FIFO-C", Name = "FIFO客户", Currency = "RMB" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var first = NewReceivable(customer.Id, "AR-1", 100m, new DateTime(2026, 7, 1));
        var second = NewReceivable(customer.Id, "AR-2", 200m, new DateTime(2026, 7, 2));
        var third = NewReceivable(customer.Id, "AR-3", 300m, new DateTime(2026, 7, 3));
        db.FinanceRecords.AddRange(first, second, third);
        await db.SaveChangesAsync();
        db.FinanceRecordLines.AddRange(
            NewLine(first.Id, "fifo:ar:1", 100m),
            NewLine(second.Id, "fifo:ar:2", 200m),
            NewLine(third.Id, "fifo:ar:3", 300m));
        var payment = new Payment
        {
            No = "REC-FIFO",
            Direction = "receive",
            CustomerId = customer.Id,
            CounterpartyType = "customer",
            Amount = 700m,
            Currency = "RMB",
            PaymentDate = new DateTime(2026, 7, 10),
            Status = "posted"
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var service = new FifoSettlementService(db, new AuditTrailService(db));
        await service.ApplyCustomerReceiptAsync(payment.Id);

        Assert.Equal(100m, first.PaidAmount);
        Assert.Equal(200m, second.PaidAmount);
        Assert.Equal(300m, third.PaidAmount);
        Assert.All(new[] { first, second, third }, record => Assert.Equal("done", record.Status));
        var allocations = await db.PaymentAllocations.OrderBy(x => x.AllocationOrder).ToListAsync();
        Assert.Equal(new[] { 100m, 200m, 300m }, allocations.Select(x => x.Amount));
        var advance = await db.CustomerAdvances.SingleAsync();
        Assert.Equal(100m, advance.OriginalAmount);
        Assert.Equal(100m, advance.AvailableAmount);
    }

    [Fact]
    public async Task CustomerReceipt_PartiallyPaysSecondOldestLine()
    {
        await using var db = TestDbFactory.Create();
        var customer = new Customer { Code = "FIFO-C2", Name = "FIFO客户二", Currency = "RMB" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var first = NewReceivable(customer.Id, "AR-A", 100m, new DateTime(2026, 7, 1));
        var second = NewReceivable(customer.Id, "AR-B", 200m, new DateTime(2026, 7, 2));
        var third = NewReceivable(customer.Id, "AR-C", 300m, new DateTime(2026, 7, 3));
        db.FinanceRecords.AddRange(first, second, third);
        await db.SaveChangesAsync();
        db.FinanceRecordLines.AddRange(
            NewLine(first.Id, "fifo:partial:1", 100m),
            NewLine(second.Id, "fifo:partial:2", 200m),
            NewLine(third.Id, "fifo:partial:3", 300m));
        var payment = new Payment
        {
            No = "REC-PARTIAL",
            Direction = "receive",
            CustomerId = customer.Id,
            CounterpartyType = "customer",
            Amount = 250m,
            Currency = "RMB",
            Status = "posted"
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        await new FifoSettlementService(db, new AuditTrailService(db)).ApplyCustomerReceiptAsync(payment.Id);

        Assert.Equal(100m, first.PaidAmount);
        Assert.Equal(150m, second.PaidAmount);
        Assert.Equal(0m, third.PaidAmount);
        Assert.Equal("done", first.Status);
        Assert.Equal("partial", second.Status);
        Assert.Equal("pending", third.Status);
        Assert.Empty(db.CustomerAdvances);
    }

    [Fact]
    public async Task SupplierPayment_DoesNotCrossProductAndLogisticsCounterpartyTypes()
    {
        await using var db = TestDbFactory.Create();
        var supplier = new Supplier { Code = "FIFO-S", Name = "商品供应商" };
        var provider = new LogisticsProvider { Code = "FIFO-L", Name = "物流服务商", Status = "active" };
        db.AddRange(supplier, provider);
        await db.SaveChangesAsync();

        var productPayable = NewPayable("AP-P", 100m, "product_supplier", supplier.Id, null, new DateTime(2026, 7, 1));
        var logisticsPayable = NewPayable("AP-L", 100m, "logistics_provider", null, provider.Id, new DateTime(2026, 7, 1));
        db.FinanceRecords.AddRange(productPayable, logisticsPayable);
        await db.SaveChangesAsync();
        db.FinanceRecordLines.AddRange(
            NewLine(productPayable.Id, "fifo:product", 100m),
            NewLine(logisticsPayable.Id, "fifo:logistics", 100m));
        var payment = new Payment
        {
            No = "PAY-L",
            Direction = "pay",
            LogisticsProviderId = provider.Id,
            CounterpartyType = "logistics_provider",
            Amount = 150m,
            Currency = "RMB",
            Status = "posted"
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        await new FifoSettlementService(db, new AuditTrailService(db))
            .ApplySupplierPaymentAsync(payment.Id, "logistics_provider");

        Assert.Equal(0m, productPayable.PaidAmount);
        Assert.Equal(100m, logisticsPayable.PaidAmount);
        var prepayment = await db.SupplierPrepayments.SingleAsync();
        Assert.Equal("logistics_provider", prepayment.CounterpartyType);
        Assert.Equal(provider.Id, prepayment.LogisticsProviderId);
        Assert.Equal(50m, prepayment.AvailableAmount);
    }

    [Fact]
    public async Task ReversePayment_RestoresBalancesWithoutDeletingOriginalAllocations()
    {
        await using var db = TestDbFactory.Create();
        var customer = new Customer { Code = "FIFO-R", Name = "反冲客户", Currency = "RMB" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var receivable = NewReceivable(customer.Id, "AR-R", 100m, DateTime.Today);
        db.FinanceRecords.Add(receivable);
        await db.SaveChangesAsync();
        db.FinanceRecordLines.Add(NewLine(receivable.Id, "fifo:reverse", 100m));
        var payment = new Payment
        {
            No = "REC-R",
            Direction = "receive",
            CustomerId = customer.Id,
            CounterpartyType = "customer",
            Amount = 100m,
            Currency = "RMB",
            Status = "posted"
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        var service = new FifoSettlementService(db, new AuditTrailService(db));
        await service.ApplyCustomerReceiptAsync(payment.Id);

        var reversal = await service.ReversePaymentAsync(payment.Id, "录入错误", 9);

        Assert.Equal("reversed", payment.Status);
        Assert.Equal("reversal", reversal.Status);
        Assert.Equal(payment.Id, reversal.ReversedPaymentId);
        Assert.Equal(0m, receivable.PaidAmount);
        Assert.Equal("pending", receivable.Status);
        Assert.Equal(2, await db.PaymentAllocations.CountAsync());
        Assert.Contains(await db.PaymentAllocations.ToListAsync(), x => x.AllocationType == "reverse" && x.Amount == -100m);
    }

    private static FinanceRecord NewReceivable(long customerId, string no, decimal amount, DateTime createdAt) => new()
    {
        No = no,
        RecordType = "receivable",
        TargetType = "TEST",
        TargetId = 1,
        CustomerId = customerId,
        CounterpartyType = "customer",
        Currency = "RMB",
        Amount = amount,
        Status = "pending",
        CreatedAt = createdAt
    };

    private static FinanceRecord NewPayable(string no, decimal amount, string type, long? supplierId, long? logisticsProviderId, DateTime createdAt) => new()
    {
        No = no,
        RecordType = "payable",
        TargetType = "TEST",
        TargetId = 1,
        SupplierId = supplierId,
        LogisticsProviderId = logisticsProviderId,
        CounterpartyType = type,
        Currency = "RMB",
        Amount = amount,
        Status = "pending",
        CreatedAt = createdAt
    };

    private static FinanceRecordLine NewLine(long financeRecordId, string sourceKey, decimal amount) => new()
    {
        FinanceRecordId = financeRecordId,
        SourceKey = sourceKey,
        LineType = "test",
        SourceType = "TEST",
        Amount = amount,
        Status = "pending"
    };
}
