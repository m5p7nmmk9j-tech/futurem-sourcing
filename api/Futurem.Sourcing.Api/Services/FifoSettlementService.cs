using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Futurem.Sourcing.Api.Services;

public sealed class FifoSettlementService
{
    private readonly AppDbContext _db;
    private readonly AuditTrailService _audit;

    public FifoSettlementService(AppDbContext db, AuditTrailService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task ApplyCustomerReceiptAsync(long paymentId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            var payment = await RequirePaymentAsync(paymentId, "receive", cancellationToken);
            if (!payment.CustomerId.HasValue)
                throw new BusinessRuleException("PAYMENT_CUSTOMER_REQUIRED", "客户收款缺少客户");
            if (await HasActiveAllocationsAsync(payment.Id, cancellationToken)) return;

            var remaining = RmbMoneyService.Round(payment.Amount);
            var records = await _db.FinanceRecords
                .Where(x => x.RecordType == "receivable" &&
                            x.CustomerId == payment.CustomerId.Value &&
                            x.Status != "done")
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);
            var order = 1;
            foreach (var record in records)
            {
                if (remaining <= 0m) break;
                remaining = await AllocateToRecordAsync(payment, record, remaining, order, cancellationToken);
                order = await NextAllocationOrderAsync(payment.Id, cancellationToken);
            }

            if (remaining > 0m)
            {
                _db.CustomerAdvances.Add(new CustomerAdvance
                {
                    No = NumberService.NewNo("CAR"),
                    CustomerId = payment.CustomerId.Value,
                    SourcePaymentId = payment.Id,
                    Currency = RmbMoneyService.Currency,
                    OriginalAmount = remaining,
                    AvailableAmount = remaining,
                    Status = "available",
                    CreatedAt = DateTime.Now
                });
            }

            payment.CounterpartyType = "customer";
            payment.Currency = RmbMoneyService.Currency;
            payment.Status = "posted";
            payment.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task ApplySupplierPaymentAsync(
        long paymentId,
        string counterpartyType,
        CancellationToken cancellationToken = default)
    {
        if (counterpartyType is not ("product_supplier" or "logistics_provider"))
            throw new BusinessRuleException("COUNTERPARTY_TYPE_INVALID", "供应商付款对象类型无效");

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            var payment = await RequirePaymentAsync(paymentId, "pay", cancellationToken);
            if (await HasActiveAllocationsAsync(payment.Id, cancellationToken)) return;
            IQueryable<FinanceRecord> query = _db.FinanceRecords
                .Where(x => x.RecordType == "payable" && x.CounterpartyType == counterpartyType && x.Status != "done");
            if (counterpartyType == "product_supplier")
            {
                if (!payment.SupplierId.HasValue)
                    throw new BusinessRuleException("PAYMENT_SUPPLIER_REQUIRED", "商品供应商付款缺少供应商");
                query = query.Where(x => x.SupplierId == payment.SupplierId.Value);
            }
            else
            {
                if (!payment.LogisticsProviderId.HasValue)
                    throw new BusinessRuleException("PAYMENT_LOGISTICS_PROVIDER_REQUIRED", "物流服务商付款缺少服务商");
                query = query.Where(x => x.LogisticsProviderId == payment.LogisticsProviderId.Value);
            }

            var records = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
            var remaining = RmbMoneyService.Round(payment.Amount);
            var order = 1;
            foreach (var record in records)
            {
                if (remaining <= 0m) break;
                remaining = await AllocateToRecordAsync(payment, record, remaining, order, cancellationToken);
                order = await NextAllocationOrderAsync(payment.Id, cancellationToken);
            }

            if (remaining > 0m)
            {
                _db.SupplierPrepayments.Add(new SupplierPrepayment
                {
                    No = NumberService.NewNo("ADV"),
                    SupplierId = counterpartyType == "product_supplier" ? payment.SupplierId : null,
                    LogisticsProviderId = counterpartyType == "logistics_provider" ? payment.LogisticsProviderId : null,
                    CounterpartyType = counterpartyType,
                    Currency = RmbMoneyService.Currency,
                    OriginalAmount = remaining,
                    AvailableAmount = remaining,
                    SourceType = "PAYMENT_OVERPAYMENT",
                    SourceId = payment.Id,
                    SourcePaymentId = payment.Id,
                    Status = "available",
                    CreatedAt = DateTime.Now
                });
            }

            payment.CounterpartyType = counterpartyType;
            payment.Currency = RmbMoneyService.Currency;
            payment.Status = "posted";
            payment.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<decimal> ApplyAvailableCustomerAdvanceAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var advances = await _db.CustomerAdvances
            .Where(x => x.CustomerId == customerId && x.AvailableAmount > 0m &&
                        (x.Status == "available" || x.Status == "partially_used"))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var records = await _db.FinanceRecords
            .Where(x => x.RecordType == "receivable" && x.CustomerId == customerId && x.Status != "done")
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        decimal applied = 0m;
        foreach (var record in records)
        foreach (var advance in advances)
        {
            var outstanding = FinanceBalanceService.Outstanding(record);
            if (outstanding <= 0m || advance.AvailableAmount <= 0m) continue;
            var amount = RmbMoneyService.Round(Math.Min(outstanding, advance.AvailableAmount));
            advance.AvailableAmount = RmbMoneyService.Round(advance.AvailableAmount - amount);
            RefreshAdvanceStatus(advance);
            record.PrepaymentAppliedAmount = RmbMoneyService.Round(record.PrepaymentAppliedAmount + amount);
            FinanceBalanceService.RefreshStatus(record);
            _db.CustomerAdvanceUsages.Add(new CustomerAdvanceUsage
            {
                CustomerAdvanceId = advance.Id,
                FinanceRecordId = record.Id,
                Amount = amount,
                UsageType = "apply",
                CreatedAt = DateTime.Now
            });
            applied += amount;
        }
        await _db.SaveChangesAsync(cancellationToken);
        return RmbMoneyService.Round(applied);
    }

    public async Task<decimal> ApplyAvailableSupplierPrepaymentAsync(
        long counterpartyId,
        string counterpartyType,
        CancellationToken cancellationToken = default)
    {
        var prepayments = await _db.SupplierPrepayments
            .Where(x => x.CounterpartyType == counterpartyType && x.AvailableAmount > 0m &&
                        (x.Status == "available" || x.Status == "partially_used") &&
                        (counterpartyType == "product_supplier"
                            ? x.SupplierId == counterpartyId
                            : x.LogisticsProviderId == counterpartyId))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var records = await _db.FinanceRecords
            .Where(x => x.RecordType == "payable" && x.CounterpartyType == counterpartyType && x.Status != "done" &&
                        (counterpartyType == "product_supplier"
                            ? x.SupplierId == counterpartyId
                            : x.LogisticsProviderId == counterpartyId))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        decimal applied = 0m;
        foreach (var record in records)
        foreach (var prepayment in prepayments)
        {
            var outstanding = FinanceBalanceService.Outstanding(record);
            if (outstanding <= 0m || prepayment.AvailableAmount <= 0m) continue;
            var amount = RmbMoneyService.Round(Math.Min(outstanding, prepayment.AvailableAmount));
            prepayment.AvailableAmount = RmbMoneyService.Round(prepayment.AvailableAmount - amount);
            RefreshPrepaymentStatus(prepayment);
            record.PrepaymentAppliedAmount = RmbMoneyService.Round(record.PrepaymentAppliedAmount + amount);
            FinanceBalanceService.RefreshStatus(record);
            _db.SupplierPrepaymentUsages.Add(new SupplierPrepaymentUsage
            {
                SupplierPrepaymentId = prepayment.Id,
                FinanceRecordId = record.Id,
                Amount = amount,
                UsageType = "apply",
                CreatedAt = DateTime.Now
            });
            applied += amount;
        }
        await _db.SaveChangesAsync(cancellationToken);
        return RmbMoneyService.Round(applied);
    }

    public async Task<Payment> ReversePaymentAsync(
        long paymentId,
        string reason,
        long? userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("PAYMENT_REVERSAL_REASON_REQUIRED", "付款反冲必须填写原因");
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            var payment = await _db.Payments.FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken)
                ?? throw new KeyNotFoundException("收付款记录不存在");
            if (payment.Status == "reversed")
                return await _db.Payments.FirstAsync(x => x.ReversedPaymentId == payment.Id, cancellationToken);

            var allocations = await _db.PaymentAllocations
                .Where(x => x.PaymentId == payment.Id && x.AllocationType == "apply")
                .OrderByDescending(x => x.AllocationOrder)
                .ToListAsync(cancellationToken);
            var reversal = new Payment
            {
                No = NumberService.NewNo(payment.Direction == "pay" ? "PAYR" : "RECR"),
                Direction = payment.Direction,
                FinanceRecordId = payment.FinanceRecordId,
                BankAccountId = payment.BankAccountId,
                TargetType = payment.TargetType,
                TargetId = payment.TargetId,
                CustomerId = payment.CustomerId,
                SupplierId = payment.SupplierId,
                LogisticsProviderId = payment.LogisticsProviderId,
                CounterpartyType = payment.CounterpartyType,
                PaymentMethod = payment.PaymentMethod,
                Currency = RmbMoneyService.Currency,
                Amount = -payment.Amount,
                PaymentDate = DateTime.Now,
                Status = "reversal",
                ReversedPaymentId = payment.Id,
                CreatedBy = userId,
                CreatedAt = DateTime.Now,
                Remark = reason.Trim()
            };
            _db.Payments.Add(reversal);
            await _db.SaveChangesAsync(cancellationToken);

            var order = 1;
            foreach (var allocation in allocations)
            {
                var record = await _db.FinanceRecords.FindAsync([allocation.FinanceRecordId], cancellationToken);
                if (record is null) continue;
                if (allocation.FinanceRecordLineId.HasValue)
                {
                    var line = await _db.FinanceRecordLines.FindAsync([allocation.FinanceRecordLineId.Value], cancellationToken);
                    if (line is not null)
                    {
                        line.PaidAmount = Math.Max(0m, RmbMoneyService.Round(line.PaidAmount - allocation.Amount));
                        line.Status = line.PaidAmount <= 0m ? "pending" : line.PaidAmount < line.Amount ? "partial" : "done";
                    }
                }
                record.PaidAmount = Math.Max(0m, RmbMoneyService.Round(record.PaidAmount - allocation.Amount));
                FinanceBalanceService.RefreshStatus(record);
                _db.PaymentAllocations.Add(new PaymentAllocation
                {
                    PaymentId = reversal.Id,
                    FinanceRecordId = allocation.FinanceRecordId,
                    FinanceRecordLineId = allocation.FinanceRecordLineId,
                    AllocationOrder = order++,
                    AllocationType = "reverse",
                    ReversedAllocationId = allocation.Id,
                    Amount = -allocation.Amount,
                    CreatedBy = userId,
                    CreatedAt = DateTime.Now
                });
            }

            var customerAdvance = await _db.CustomerAdvances.FirstOrDefaultAsync(x => x.SourcePaymentId == payment.Id, cancellationToken);
            if (customerAdvance is not null && customerAdvance.AvailableAmount != customerAdvance.OriginalAmount)
                throw new BusinessRuleException("PAYMENT_ADVANCE_ALREADY_USED", "该收款形成的预收款已被使用，必须先反冲预收款抵扣");
            if (customerAdvance is not null)
            {
                customerAdvance.AvailableAmount = 0m;
                customerAdvance.Status = "cancelled";
            }
            var supplierAdvance = await _db.SupplierPrepayments.FirstOrDefaultAsync(x => x.SourcePaymentId == payment.Id, cancellationToken);
            if (supplierAdvance is not null && supplierAdvance.AvailableAmount != supplierAdvance.OriginalAmount)
                throw new BusinessRuleException("PAYMENT_ADVANCE_ALREADY_USED", "该付款形成的预付款已被使用，必须先反冲预付款抵扣");
            if (supplierAdvance is not null)
            {
                supplierAdvance.AvailableAmount = 0m;
                supplierAdvance.Status = "cancelled";
            }

            payment.Status = "reversed";
            payment.UpdatedBy = userId;
            payment.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync(nameof(Payment), payment.Id, "reverse", null, new { reversal.Id }, reason, userId);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return reversal;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<decimal> AllocateToRecordAsync(
        Payment payment,
        FinanceRecord record,
        decimal remaining,
        int startingOrder,
        CancellationToken cancellationToken)
    {
        var lines = await _db.FinanceRecordLines
            .Where(x => x.FinanceRecordId == record.Id && x.Status != "done")
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        if (lines.Count == 0)
        {
            var amount = RmbMoneyService.Round(Math.Min(remaining, FinanceBalanceService.Outstanding(record)));
            if (amount <= 0m) return remaining;
            record.PaidAmount = RmbMoneyService.Round(record.PaidAmount + amount);
            FinanceBalanceService.RefreshStatus(record);
            _db.PaymentAllocations.Add(NewAllocation(payment.Id, record.Id, null, startingOrder, amount));
            await _db.SaveChangesAsync(cancellationToken);
            return RmbMoneyService.Round(remaining - amount);
        }

        var order = startingOrder;
        foreach (var line in lines)
        {
            if (remaining <= 0m) break;
            var lineOutstanding = Math.Max(0m, RmbMoneyService.Round(line.Amount - line.PaidAmount));
            var amount = RmbMoneyService.Round(Math.Min(remaining, lineOutstanding));
            if (amount <= 0m) continue;
            line.PaidAmount = RmbMoneyService.Round(line.PaidAmount + amount);
            line.Status = line.PaidAmount <= 0m ? "pending" : line.PaidAmount < line.Amount ? "partial" : "done";
            record.PaidAmount = RmbMoneyService.Round(record.PaidAmount + amount);
            FinanceBalanceService.RefreshStatus(record);
            _db.PaymentAllocations.Add(NewAllocation(payment.Id, record.Id, line.Id, order++, amount));
            remaining = RmbMoneyService.Round(remaining - amount);
        }
        await _db.SaveChangesAsync(cancellationToken);
        return remaining;
    }

    private static PaymentAllocation NewAllocation(long paymentId, long financeRecordId, long? lineId, int order, decimal amount) => new()
    {
        PaymentId = paymentId,
        FinanceRecordId = financeRecordId,
        FinanceRecordLineId = lineId,
        AllocationOrder = order,
        AllocationType = "apply",
        Amount = amount,
        CreatedAt = DateTime.Now
    };

    private async Task<Payment> RequirePaymentAsync(long paymentId, string direction, CancellationToken cancellationToken)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken)
            ?? throw new KeyNotFoundException("收付款记录不存在");
        if (payment.Direction != direction)
            throw new BusinessRuleException("PAYMENT_DIRECTION_INVALID", "收付款方向不正确");
        if (payment.Amount <= 0m)
            throw new BusinessRuleException("PAYMENT_AMOUNT_INVALID", "收付款金额必须大于零");
        if (payment.Status == "reversed")
            throw new BusinessRuleException("PAYMENT_ALREADY_REVERSED", "该收付款已反冲");
        return payment;
    }

    private async Task<bool> HasActiveAllocationsAsync(long paymentId, CancellationToken cancellationToken)
        => await _db.PaymentAllocations.AnyAsync(x => x.PaymentId == paymentId && x.AllocationType == "apply", cancellationToken);

    private async Task<int> NextAllocationOrderAsync(long paymentId, CancellationToken cancellationToken)
        => (await _db.PaymentAllocations.Where(x => x.PaymentId == paymentId).MaxAsync(x => (int?)x.AllocationOrder, cancellationToken) ?? 0) + 1;

    private static void RefreshAdvanceStatus(CustomerAdvance advance)
        => advance.Status = advance.AvailableAmount <= 0m ? "used" : advance.AvailableAmount < advance.OriginalAmount ? "partially_used" : "available";

    private static void RefreshPrepaymentStatus(SupplierPrepayment prepayment)
        => prepayment.Status = prepayment.AvailableAmount <= 0m ? "used" : prepayment.AvailableAmount < prepayment.OriginalAmount ? "partially_used" : "available";

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
        => _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;
}
