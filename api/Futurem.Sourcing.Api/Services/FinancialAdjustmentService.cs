using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Futurem.Sourcing.Api.Services;

public sealed record FinancialAdjustmentCreateInput(
    long FinanceRecordId,
    string AdjustmentType,
    decimal Amount,
    string Reason,
    string SourceType,
    long? SourceId,
    long? FinanceRecordLineId = null,
    long? QcOrderId = null,
    long? QcOrderLineId = null,
    long? ShipmentId = null,
    long? ShipmentExpenseId = null);

public sealed class FinancialAdjustmentService
{
    private static readonly string[] AllowedTypes =
    [
        "supplier_refund_or_credit",
        "supplemental_payable",
        "customer_receivable_adjustment",
        "logistics_cost_adjustment"
    ];

    private readonly AppDbContext _db;
    private readonly FinanceDocumentService _finance;
    private readonly AuditTrailService _audit;

    public FinancialAdjustmentService(
        AppDbContext db,
        FinanceDocumentService finance,
        AuditTrailService audit)
    {
        _db = db;
        _finance = finance;
        _audit = audit;
    }

    public async Task<FinancialAdjustment> CreateAsync(
        FinancialAdjustmentCreateInput input,
        long? userId,
        CancellationToken cancellationToken = default)
    {
        if (!AllowedTypes.Contains(input.AdjustmentType))
            throw new BusinessRuleException("FINANCIAL_ADJUSTMENT_TYPE_INVALID", "财务调整类型无效");
        if (input.Amount == 0m)
            throw new BusinessRuleException("FINANCIAL_ADJUSTMENT_AMOUNT_REQUIRED", "调整金额不能为0");
        if (string.IsNullOrWhiteSpace(input.Reason))
            throw new BusinessRuleException("FINANCIAL_ADJUSTMENT_REASON_REQUIRED", "财务调整必须填写原因");

        var financeRecord = await _db.FinanceRecords.FirstOrDefaultAsync(
            x => x.Id == input.FinanceRecordId,
            cancellationToken) ?? throw new KeyNotFoundException("财务单不存在");

        ValidateAdjustmentDirection(financeRecord, input.AdjustmentType, input.Amount);

        var now = DateTime.Now;
        var adjustment = new FinancialAdjustment
        {
            FinanceRecordId = financeRecord.Id,
            FinanceRecordLineId = input.FinanceRecordLineId,
            QcOrderId = input.QcOrderId,
            QcOrderLineId = input.QcOrderLineId,
            ShipmentId = input.ShipmentId,
            ShipmentExpenseId = input.ShipmentExpenseId,
            AdjustmentType = input.AdjustmentType,
            SourceType = input.SourceType?.Trim() ?? string.Empty,
            SourceId = input.SourceId,
            SourceKey = $"adjustment:{Guid.NewGuid():N}",
            Status = "draft",
            AdjustmentDate = now,
            OriginalAmount = financeRecord.Amount,
            Amount = RmbMoneyService.Round(input.Amount),
            ResultAmount = RmbMoneyService.Round(financeRecord.Amount + input.Amount),
            Reason = input.Reason.Trim(),
            Remark = input.Reason.Trim(),
            CreatedBy = userId,
            CreatedAt = now
        };

        if (adjustment.ResultAmount < 0m)
            throw new BusinessRuleException("FINANCIAL_ADJUSTMENT_RESULT_NEGATIVE", "调整后财务金额不能小于0");

        _db.FinancialAdjustments.Add(adjustment);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(
            nameof(FinancialAdjustment),
            adjustment.Id,
            "create",
            null,
            new { adjustment.AdjustmentType, adjustment.Amount, adjustment.ResultAmount },
            adjustment.Reason,
            userId);
        return adjustment;
    }

    public async Task<FinancialAdjustment> ApproveAsync(
        long adjustmentId,
        long? userId,
        CancellationToken cancellationToken = default)
    {
        var adjustment = await RequireAsync(adjustmentId, cancellationToken);
        if (adjustment.Status == "approved" || adjustment.Status == "applied") return adjustment;
        if (adjustment.Status != "draft")
            throw new BusinessRuleException("FINANCIAL_ADJUSTMENT_STATUS_INVALID", "只有草稿调整单可以审批");

        adjustment.Status = "approved";
        adjustment.ApprovedBy = userId;
        adjustment.ApprovedAt = DateTime.Now;
        adjustment.UpdatedBy = userId;
        adjustment.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(
            nameof(FinancialAdjustment),
            adjustment.Id,
            "approve",
            new { status = "draft" },
            new { adjustment.Status, adjustment.ApprovedAt },
            adjustment.Reason,
            userId);
        return adjustment;
    }

    public async Task<FinancialAdjustment> ApplyAsync(
        long adjustmentId,
        long? userId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            var adjustment = await RequireForUpdateAsync(adjustmentId, cancellationToken);
            if (adjustment.Status == "applied")
            {
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return adjustment;
            }
            if (adjustment.Status != "approved")
                throw new BusinessRuleException("FINANCIAL_ADJUSTMENT_NOT_APPROVED", "财务调整单必须先审批再应用");

            var record = await _db.FinanceRecords.FirstOrDefaultAsync(
                x => x.Id == adjustment.FinanceRecordId,
                cancellationToken) ?? throw new KeyNotFoundException("财务单不存在");
            var lineSourceKey = $"adjustment:{adjustment.Id}";
            var existingLine = await _db.FinanceRecordLines.FirstOrDefaultAsync(
                x => x.SourceKey == lineSourceKey,
                cancellationToken);
            if (existingLine is null)
            {
                _db.FinanceRecordLines.Add(new FinanceRecordLine
                {
                    FinanceRecordId = record.Id,
                    SourceKey = lineSourceKey,
                    LineType = "adjustment",
                    SourceType = nameof(FinancialAdjustment),
                    SourceId = adjustment.Id,
                    Quantity = 1m,
                    UnitPrice = adjustment.Amount,
                    Amount = adjustment.Amount,
                    PaidAmount = 0m,
                    Description = adjustment.Reason,
                    Status = adjustment.Amount < 0m ? "done" : "pending",
                    CreatedBy = userId,
                    CreatedAt = DateTime.Now
                });
                await _db.SaveChangesAsync(cancellationToken);
            }

            await _finance.RecalculateAsync(record.Id, cancellationToken);
            await TransferOverpaymentAsync(record, adjustment, userId, cancellationToken);

            adjustment.Status = "applied";
            adjustment.AppliedBy = userId;
            adjustment.AppliedAt = DateTime.Now;
            adjustment.ResultAmount = record.Amount;
            adjustment.UpdatedBy = userId;
            adjustment.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync(
                nameof(FinancialAdjustment),
                adjustment.Id,
                "apply",
                new { adjustment.OriginalAmount },
                new { adjustment.ResultAmount, adjustment.Status },
                adjustment.Reason,
                userId);

            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return adjustment;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<FinancialAdjustment> CancelAsync(
        long adjustmentId,
        string reason,
        long? userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("FINANCIAL_ADJUSTMENT_CANCEL_REASON_REQUIRED", "取消调整单必须填写原因");
        var adjustment = await RequireAsync(adjustmentId, cancellationToken);
        if (adjustment.Status == "applied")
            throw new BusinessRuleException("FINANCIAL_ADJUSTMENT_ALREADY_APPLIED", "已应用调整不能直接取消，必须创建反向调整单");
        if (adjustment.Status == "cancelled") return adjustment;

        adjustment.Status = "cancelled";
        adjustment.CancelledBy = userId;
        adjustment.CancelledAt = DateTime.Now;
        adjustment.CancelReason = reason.Trim();
        adjustment.UpdatedBy = userId;
        adjustment.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(
            nameof(FinancialAdjustment),
            adjustment.Id,
            "cancel",
            null,
            new { adjustment.Status, adjustment.CancelReason },
            reason,
            userId);
        return adjustment;
    }

    private async Task TransferOverpaymentAsync(
        FinanceRecord record,
        FinancialAdjustment adjustment,
        long? userId,
        CancellationToken cancellationToken)
    {
        var settled = RmbMoneyService.Round(record.PaidAmount + record.PrepaymentAppliedAmount);
        var overpaid = RmbMoneyService.Round(Math.Max(0m, settled - record.Amount));
        var notTransferred = RmbMoneyService.Round(Math.Max(0m, overpaid - record.OverpaymentTransferredAmount));
        if (notTransferred <= 0m) return;

        if (record.RecordType == "payable")
        {
            var existing = await _db.SupplierPrepayments.FirstOrDefaultAsync(
                x => x.SourceType == "FINANCIAL_ADJUSTMENT" && x.SourceId == adjustment.Id,
                cancellationToken);
            if (existing is null)
            {
                _db.SupplierPrepayments.Add(new SupplierPrepayment
                {
                    No = NumberService.NewNo("ADV"),
                    SupplierId = record.SupplierId,
                    LogisticsProviderId = record.LogisticsProviderId,
                    CounterpartyType = record.CounterpartyType,
                    Currency = RmbMoneyService.Currency,
                    OriginalAmount = notTransferred,
                    AvailableAmount = notTransferred,
                    SourceType = "FINANCIAL_ADJUSTMENT",
                    SourceId = adjustment.Id,
                    Status = "available",
                    CreatedBy = userId,
                    CreatedAt = DateTime.Now,
                    Remark = adjustment.Reason
                });
            }
        }
        else if (record.RecordType == "receivable" && record.CustomerId.HasValue)
        {
            var existing = await _db.CustomerAdvances.FirstOrDefaultAsync(
                x => x.SourceAdjustmentId == adjustment.Id,
                cancellationToken);
            if (existing is null)
            {
                _db.CustomerAdvances.Add(new CustomerAdvance
                {
                    No = NumberService.NewNo("CAR"),
                    CustomerId = record.CustomerId.Value,
                    Currency = RmbMoneyService.Currency,
                    OriginalAmount = notTransferred,
                    AvailableAmount = notTransferred,
                    Status = "available",
                    SourceAdjustmentId = adjustment.Id,
                    CreatedBy = userId,
                    CreatedAt = DateTime.Now,
                    Remark = adjustment.Reason
                });
            }
        }

        record.OverpaymentTransferredAmount = RmbMoneyService.Round(record.OverpaymentTransferredAmount + notTransferred);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateAdjustmentDirection(
        FinanceRecord record,
        string adjustmentType,
        decimal amount)
    {
        if (adjustmentType == "supplier_refund_or_credit" && (record.RecordType != "payable" || amount >= 0m))
            throw new BusinessRuleException("FINANCIAL_ADJUSTMENT_DIRECTION_INVALID", "供应商退款或冲减必须是应付负数调整");
        if (adjustmentType == "supplemental_payable" && (record.RecordType != "payable" || amount <= 0m))
            throw new BusinessRuleException("FINANCIAL_ADJUSTMENT_DIRECTION_INVALID", "补充应付必须是应付正数调整");
        if (adjustmentType == "customer_receivable_adjustment" && record.RecordType != "receivable")
            throw new BusinessRuleException("FINANCIAL_ADJUSTMENT_DIRECTION_INVALID", "客户应收调整只能用于应收单");
        if (adjustmentType == "logistics_cost_adjustment" &&
            (record.RecordType != "payable" || record.CounterpartyType != "logistics_provider"))
            throw new BusinessRuleException("FINANCIAL_ADJUSTMENT_DIRECTION_INVALID", "物流成本调整只能用于物流服务商应付");
    }

    private async Task<FinancialAdjustment> RequireAsync(long id, CancellationToken cancellationToken)
        => await _db.FinancialAdjustments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
           ?? throw new KeyNotFoundException("财务调整单不存在");

    private async Task<FinancialAdjustment> RequireForUpdateAsync(long id, CancellationToken cancellationToken)
    {
        if (!_db.Database.IsRelational()) return await RequireAsync(id, cancellationToken);
        return await _db.FinancialAdjustments
            .FromSqlInterpolated($"SELECT * FROM `financial_adjustments` WHERE `id` = {id} AND `is_deleted` = 0 FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("财务调整单不存在");
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
        => _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;
}
