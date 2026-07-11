using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Futurem.Sourcing.Api.Services;

public sealed record QcLineResult(
    long ReceivingLineId,
    decimal ArrivedQuantity,
    decimal QualifiedQuantity,
    decimal UnqualifiedQuantity,
    decimal ReturnedQuantity,
    decimal PendingQuantity,
    decimal AcceptedQuantity);

public sealed class QcConfirmationService
{
    private readonly AppDbContext _db;
    private readonly AuditTrailService _audit;

    public QcConfirmationService(AppDbContext db, AuditTrailService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<QcOrder> CreateDraftAsync(long receivingOrderId, long? userId)
    {
        var receiving = await _db.ReceivingOrders.FirstOrDefaultAsync(x => x.Id == receivingOrderId)
            ?? throw new KeyNotFoundException("收货单不存在");
        var existing = await _db.QcOrders.FirstOrDefaultAsync(x => x.ReceivingOrderId == receivingOrderId);
        if (existing is not null)
            throw new BusinessRuleException(
                "RECEIVING_ALREADY_HAS_QC",
                "一张收货单只能有一张验货单，请在原验货单中解锁并修改");

        var hasLines = await _db.DocumentLines.AnyAsync(x =>
            x.DocumentType == "RCV" && x.DocumentId == receiving.Id && !x.IsDeleted);
        if (!hasLines)
            throw new BusinessRuleException("RECEIVING_LINES_REQUIRED", "收货单没有可验货商品");

        var qc = new QcOrder
        {
            No = NumberService.NewNo("QC"),
            PurchaseOrderId = receiving.PurchaseOrderId,
            ReceivingOrderId = receiving.Id,
            QcDate = DateTime.Today,
            Status = "draft",
            Result = "pending",
            CreatedBy = userId,
            CreatedAt = DateTime.Now
        };
        _db.QcOrders.Add(qc);
        receiving.Status = "qc_pending";
        receiving.UpdatedBy = userId;
        receiving.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(
            nameof(QcOrder),
            qc.Id,
            "create",
            null,
            new { qc.ReceivingOrderId, qc.Status },
            "根据收货单创建验货单",
            userId);
        return qc;
    }

    public async Task<QcOrder> ConfirmAsync(
        long qcOrderId,
        IReadOnlyCollection<QcLineResult> results,
        long? userId)
    {
        if (results.Count == 0)
            throw new BusinessRuleException("QC_LINES_REQUIRED", "验货单至少需要一个商品结果");
        if (results.GroupBy(x => x.ReceivingLineId).Any(x => x.Count() > 1))
            throw new BusinessRuleException("QC_LINE_DUPLICATED", "同一收货商品不能重复填写验货结果");
        foreach (var result in results) ValidateResult(result);

        await using var transaction = await BeginTransactionIfSupportedAsync();
        try
        {
            var qc = await _db.QcOrders.FirstOrDefaultAsync(x => x.Id == qcOrderId)
                ?? throw new KeyNotFoundException("验货单不存在");
            if (qc.Status == "confirmed") return qc;
            if (qc.Status is not ("draft" or "unlocked"))
                throw new BusinessRuleException("QC_STATUS_INVALID", "只有草稿或已解锁验货单可以确认");
            if (!qc.ReceivingOrderId.HasValue)
                throw new BusinessRuleException("QC_RECEIVING_REQUIRED", "验货单缺少收货单来源");

            var receiving = await _db.ReceivingOrders.FirstOrDefaultAsync(x => x.Id == qc.ReceivingOrderId.Value)
                ?? throw new KeyNotFoundException("收货单不存在");
            var receivingLines = await _db.DocumentLines
                .Where(x => x.DocumentType == "RCV" && x.DocumentId == receiving.Id && !x.IsDeleted)
                .OrderBy(x => x.SortNo)
                .ThenBy(x => x.Id)
                .ToListAsync();
            var inputIds = results.Select(x => x.ReceivingLineId).OrderBy(x => x).ToList();
            var sourceIds = receivingLines.Select(x => x.Id).OrderBy(x => x).ToList();
            if (!inputIds.SequenceEqual(sourceIds))
                throw new BusinessRuleException("QC_MUST_COVER_ALL_RECEIVING_LINES", "一张验货单必须覆盖该收货单全部商品");

            var resultByLine = results.ToDictionary(x => x.ReceivingLineId);
            foreach (var source in receivingLines)
            {
                var result = resultByLine[source.Id];
                if (RmbMoneyService.Round(result.ArrivedQuantity) != RmbMoneyService.Round(source.Quantity))
                {
                    throw new BusinessRuleException(
                        "QC_ARRIVED_MISMATCH",
                        "验货到货数量必须等于收货单临时点数",
                        new { receivingLineId = source.Id, receivingQuantity = source.Quantity, result.ArrivedQuantity });
                }
            }

            var version = qc.ConfirmationVersion + 1;
            var existingLines = await _db.QcOrderLines
                .Where(x => x.QcOrderId == qc.Id)
                .ToDictionaryAsync(x => x.ReceivingLineId);
            var confirmedLines = new List<QcOrderLine>();

            foreach (var source in receivingLines)
            {
                var result = resultByLine[source.Id];
                if (!source.OrderProductId.HasValue || source.OrderProductId.Value <= 0)
                    throw new BusinessRuleException("ORDER_PRODUCT_LINK_REQUIRED", "收货商品缺少订单商品来源");
                var supplierId = source.SupplierId ?? receiving.SupplierId;
                if (!supplierId.HasValue || supplierId.Value <= 0)
                    throw new BusinessRuleException("SUPPLIER_REQUIRED", "收货商品缺少供应商");

                if (!existingLines.TryGetValue(source.Id, out var line))
                {
                    line = new QcOrderLine
                    {
                        QcOrderId = qc.Id,
                        ReceivingOrderId = receiving.Id,
                        ReceivingLineId = source.Id,
                        DeliveryNoticeLineId = source.DeliveryNoticeLineId,
                        PurchaseOrderId = receiving.PurchaseOrderId,
                        PurchaseOrderLineId = source.SourceDocumentLineId,
                        OrderProductId = source.OrderProductId.Value,
                        SupplierId = supplierId.Value,
                        WarehouseId = source.WarehouseId ?? receiving.WarehouseId,
                        CreatedBy = userId,
                        CreatedAt = DateTime.Now
                    };
                    _db.QcOrderLines.Add(line);
                }

                line.ConfirmationVersion = version;
                line.ArrivedQuantity = RmbMoneyService.Round(result.ArrivedQuantity);
                line.QualifiedQuantity = RmbMoneyService.Round(result.QualifiedQuantity);
                line.UnqualifiedQuantity = RmbMoneyService.Round(result.UnqualifiedQuantity);
                line.ReturnedQuantity = RmbMoneyService.Round(result.ReturnedQuantity);
                line.PendingQuantity = RmbMoneyService.Round(result.PendingQuantity);
                line.AcceptedQuantity = RmbMoneyService.Round(result.AcceptedQuantity);
                line.PurchaseUnitPrice = RmbMoneyService.Round(
                    source.PurchaseUnitPriceSnapshot > 0 ? source.PurchaseUnitPriceSnapshot : source.UnitPrice);
                line.PayableAmount = RmbMoneyService.Round(line.AcceptedQuantity * line.PurchaseUnitPrice);
                line.UpdatedBy = userId;
                line.UpdatedAt = DateTime.Now;
                confirmedLines.Add(line);
            }

            await _db.SaveChangesAsync();
            foreach (var line in confirmedLines)
                await EnsurePayableAsync(qc, line, version, userId);

            var before = new { qc.Status, qc.Result, qc.ConfirmationVersion };
            qc.ConfirmationVersion = version;
            qc.Status = "confirmed";
            qc.Result = confirmedLines.All(x => x.AcceptedQuantity == x.ArrivedQuantity)
                ? "accepted_all"
                : "accepted_partial";
            qc.ConfirmedAt = DateTime.Now;
            qc.UpdatedBy = userId;
            qc.UpdatedAt = DateTime.Now;
            receiving.Status = "qc_confirmed";
            receiving.UpdatedBy = userId;
            receiving.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            await _audit.WriteAsync(
                nameof(QcOrder),
                qc.Id,
                "confirm",
                before,
                new
                {
                    qc.Status,
                    qc.Result,
                    qc.ConfirmationVersion,
                    acceptedQuantity = confirmedLines.Sum(x => x.AcceptedQuantity),
                    payableAmount = confirmedLines.Sum(x => x.PayableAmount)
                },
                "确认最终实际接受数量并生成供应商应付",
                userId);

            if (transaction is not null) await transaction.CommitAsync();
            return qc;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<QcOrder> UnlockAsync(long qcOrderId, string reason, long? userId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("QC_UNLOCK_REASON_REQUIRED", "解锁验货单必须填写原因");

        var qc = await _db.QcOrders.FirstOrDefaultAsync(x => x.Id == qcOrderId)
            ?? throw new KeyNotFoundException("验货单不存在");
        if (qc.Status == "unlocked") return qc;
        if (qc.Status != "confirmed")
            throw new BusinessRuleException("QC_NOT_CONFIRMED", "只有已确认验货单可以解锁");

        var before = new { qc.Status, qc.Result, qc.ConfirmationVersion, qc.ConfirmedAt };
        qc.Status = "unlocked";
        qc.Result = "pending";
        qc.UnlockedAt = DateTime.Now;
        qc.UnlockedBy = userId;
        qc.UnlockReason = reason.Trim();
        qc.UpdatedBy = userId;
        qc.UpdatedAt = DateTime.Now;
        if (qc.ReceivingOrderId.HasValue)
        {
            var receiving = await _db.ReceivingOrders.FirstOrDefaultAsync(x => x.Id == qc.ReceivingOrderId.Value);
            if (receiving is not null)
            {
                receiving.Status = "qc_reopened";
                receiving.UpdatedBy = userId;
                receiving.UpdatedAt = DateTime.Now;
            }
        }
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(
            nameof(QcOrder),
            qc.Id,
            "unlock",
            before,
            new { qc.Status, qc.UnlockedAt, qc.UnlockedBy, qc.UnlockReason },
            reason,
            userId);
        return qc;
    }

    private async Task EnsurePayableAsync(QcOrder qc, QcOrderLine line, int version, long? userId)
    {
        var sourceKey = $"qc:{qc.Id}:line:{line.Id}";
        var payable = await _db.FinanceRecords.FirstOrDefaultAsync(x => x.SourceKey == sourceKey);
        if (payable is null)
        {
            payable = new FinanceRecord
            {
                No = NumberService.NewNo("AP"),
                RecordType = "payable",
                TargetType = "QC_ACCEPTED",
                TargetId = line.Id,
                SupplierId = line.SupplierId,
                Currency = RmbMoneyService.Currency,
                Amount = line.PayableAmount,
                QcOrderId = qc.Id,
                QcOrderLineId = line.Id,
                SourceKey = sourceKey,
                RecordDate = DateTime.Today,
                Status = "pending",
                CreatedBy = userId,
                CreatedAt = DateTime.Now,
                Remark = $"验货单 {qc.No} 最终接受数量应付"
            };
            FinanceBalanceService.RefreshStatus(payable);
            _db.FinanceRecords.Add(payable);
            await _db.SaveChangesAsync();
            return;
        }

        var settled = FinanceBalanceService.EffectiveSettled(payable);
        if (settled <= 0m)
        {
            payable.Amount = line.PayableAmount;
            payable.SupplierId = line.SupplierId;
            payable.QcOrderId = qc.Id;
            payable.QcOrderLineId = line.Id;
            payable.UpdatedBy = userId;
            payable.UpdatedAt = DateTime.Now;
            FinanceBalanceService.RefreshStatus(payable);
            await _db.SaveChangesAsync();
            return;
        }

        if (line.PayableAmount < payable.Amount)
        {
            var adjustmentKey = $"qc:{qc.Id}:line:{line.Id}:version:{version}:credit";
            var exists = await _db.FinancialAdjustments.AnyAsync(x => x.SourceKey == adjustmentKey);
            if (!exists)
            {
                _db.FinancialAdjustments.Add(new FinancialAdjustment
                {
                    FinanceRecordId = payable.Id,
                    QcOrderId = qc.Id,
                    QcOrderLineId = line.Id,
                    AdjustmentType = "supplier_refund_or_credit",
                    SourceKey = adjustmentKey,
                    Amount = RmbMoneyService.Round(payable.Amount - line.PayableAmount),
                    Status = "pending",
                    AdjustmentDate = DateTime.Today,
                    CreatedBy = userId,
                    CreatedAt = DateTime.Now,
                    Remark = "已付款后验货数量减少，等待供应商退款或抵扣"
                });
                await _db.SaveChangesAsync();
            }
            return;
        }

        if (line.PayableAmount > payable.Amount)
        {
            var supplementKey = $"qc:{qc.Id}:line:{line.Id}:version:{version}:supplement";
            var exists = await _db.FinanceRecords.AnyAsync(x => x.SourceKey == supplementKey);
            if (!exists)
            {
                var supplement = new FinanceRecord
                {
                    No = NumberService.NewNo("AP"),
                    RecordType = "payable",
                    TargetType = "QC_ACCEPTED_SUPPLEMENT",
                    TargetId = line.Id,
                    SupplierId = line.SupplierId,
                    Currency = RmbMoneyService.Currency,
                    Amount = RmbMoneyService.Round(line.PayableAmount - payable.Amount),
                    QcOrderId = qc.Id,
                    QcOrderLineId = line.Id,
                    SourceKey = supplementKey,
                    RecordDate = DateTime.Today,
                    Status = "pending",
                    CreatedBy = userId,
                    CreatedAt = DateTime.Now,
                    Remark = $"验货单 {qc.No} 重验补充应付"
                };
                FinanceBalanceService.RefreshStatus(supplement);
                _db.FinanceRecords.Add(supplement);
                await _db.SaveChangesAsync();
            }
        }
    }

    private static void ValidateResult(QcLineResult result)
    {
        var values = new[]
        {
            result.ArrivedQuantity,
            result.QualifiedQuantity,
            result.UnqualifiedQuantity,
            result.ReturnedQuantity,
            result.PendingQuantity,
            result.AcceptedQuantity
        };
        if (values.Any(x => x < 0))
            throw new BusinessRuleException("QC_QUANTITY_NEGATIVE", "验货数量不能为负数");

        var classified = RmbMoneyService.Round(
            result.QualifiedQuantity +
            result.UnqualifiedQuantity +
            result.ReturnedQuantity +
            result.PendingQuantity);
        if (RmbMoneyService.Round(result.ArrivedQuantity) != classified)
        {
            throw new BusinessRuleException(
                "QC_QUANTITY_EQUATION_INVALID",
                "到货数量必须等于合格、不合格、退回和待处理数量之和",
                new { result.ReceivingLineId, result.ArrivedQuantity, classifiedQuantity = classified });
        }
        if (RmbMoneyService.Round(result.AcceptedQuantity) > RmbMoneyService.Round(result.ArrivedQuantity))
            throw new BusinessRuleException("QC_ACCEPTED_OVER_ARRIVED", "最终接受数量不能超过到货数量");
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync()
        => _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;
}
