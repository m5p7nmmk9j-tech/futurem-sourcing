using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public sealed record FinanceLineInput(
    string SourceKey,
    string LineType,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount,
    string? Description,
    string SourceType = "",
    long? SourceId = null,
    long? OrderProductId = null);

public sealed class FinanceDocumentService
{
    private readonly AppDbContext _db;

    public FinanceDocumentService(AppDbContext db) => _db = db;

    public async Task<FinanceRecord> EnsureCustomerReceivableForContainerAsync(long containerLoadId)
    {
        var container = await _db.ContainerLoads.FirstOrDefaultAsync(x => x.Id == containerLoadId)
            ?? throw new KeyNotFoundException("装柜单不存在");
        if (!container.CustomerId.HasValue)
            throw new BusinessRuleException("CONTAINER_CUSTOMER_REQUIRED", "装柜单缺少客户");

        var sources = await _db.ContainerLoadSources
            .Where(x => x.ContainerLoadId == containerLoadId && x.ActualQuantity > 0m)
            .OrderBy(x => x.Id)
            .ToListAsync();
        if (sources.Count == 0)
            throw new BusinessRuleException("ACTUAL_LOAD_REQUIRED", "没有实际装柜商品，不能生成客户应收");

        var lines = sources.Select(source => new FinanceLineInput(
            $"container:{containerLoadId}:source:{source.Id}:goods",
            "goods",
            source.ActualQuantity,
            source.SalesUnitPrice,
            RmbMoneyService.Round(source.ActualQuantity * source.SalesUnitPrice),
            $"装柜单 {container.No} 商品货款",
            "CONTAINER_LOAD_SOURCE",
            source.Id,
            source.OrderProductId)).ToList();

        return await EnsureReceivableAsync(
            $"container:{containerLoadId}:goods",
            "CONTAINER_LOAD",
            containerLoadId,
            container.CustomerId.Value,
            lines);
    }

    public async Task<FinanceRecord> EnsureReceivableAsync(
        string sourceKey,
        string targetType,
        long targetId,
        long customerId,
        IReadOnlyCollection<FinanceLineInput> lines,
        CancellationToken cancellationToken = default)
    {
        var record = await _db.FinanceRecords.FirstOrDefaultAsync(x => x.SourceKey == sourceKey, cancellationToken);
        if (record is null)
        {
            record = new FinanceRecord
            {
                No = NumberService.NewNo("AR"),
                RecordType = "receivable",
                TargetType = targetType,
                TargetId = targetId,
                CustomerId = customerId,
                SupplierId = null,
                LogisticsProviderId = null,
                CounterpartyType = "customer",
                Currency = RmbMoneyService.Currency,
                SourceKey = sourceKey,
                RecordDate = DateTime.Now,
                Status = "pending",
                CreatedAt = DateTime.Now
            };
            _db.FinanceRecords.Add(record);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            if (record.RecordType != "receivable" || record.CustomerId != customerId)
                throw new BusinessRuleException("FINANCE_SOURCE_CONFLICT", "财务来源键已被其他往来单占用");
            record.CounterpartyType = "customer";
            record.Currency = RmbMoneyService.Currency;
        }

        await EnsureLinesAsync(record, lines, cancellationToken);
        await RecalculateAsync(record.Id, cancellationToken);
        return record;
    }

    public async Task<FinanceRecord> EnsurePayableAsync(
        string sourceKey,
        string targetType,
        long targetId,
        long supplierId,
        IReadOnlyCollection<FinanceLineInput> lines,
        CancellationToken cancellationToken = default)
    {
        var record = await _db.FinanceRecords.FirstOrDefaultAsync(x => x.SourceKey == sourceKey, cancellationToken);
        if (record is null)
        {
            record = new FinanceRecord
            {
                No = NumberService.NewNo("AP"),
                RecordType = "payable",
                TargetType = targetType,
                TargetId = targetId,
                SupplierId = supplierId,
                LogisticsProviderId = null,
                CounterpartyType = "product_supplier",
                Currency = RmbMoneyService.Currency,
                SourceKey = sourceKey,
                RecordDate = DateTime.Now,
                Status = "pending",
                CreatedAt = DateTime.Now
            };
            _db.FinanceRecords.Add(record);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            if (record.RecordType != "payable" || record.SupplierId != supplierId)
                throw new BusinessRuleException("FINANCE_SOURCE_CONFLICT", "财务来源键已被其他往来单占用");
            record.CounterpartyType = "product_supplier";
            record.Currency = RmbMoneyService.Currency;
        }

        await EnsureLinesAsync(record, lines, cancellationToken);
        await RecalculateAsync(record.Id, cancellationToken);
        return record;
    }

    public async Task<FinanceRecord> EnsureLogisticsProviderPayableAsync(
        string sourceKey,
        string targetType,
        long targetId,
        long logisticsProviderId,
        IReadOnlyCollection<FinanceLineInput> lines,
        CancellationToken cancellationToken = default)
    {
        var record = await _db.FinanceRecords.FirstOrDefaultAsync(x => x.SourceKey == sourceKey, cancellationToken);
        if (record is null)
        {
            record = new FinanceRecord
            {
                No = NumberService.NewNo("AP"),
                RecordType = "payable",
                TargetType = targetType,
                TargetId = targetId,
                SupplierId = null,
                LogisticsProviderId = logisticsProviderId,
                CounterpartyType = "logistics_provider",
                Currency = RmbMoneyService.Currency,
                SourceKey = sourceKey,
                RecordDate = DateTime.Now,
                Status = "pending",
                CreatedAt = DateTime.Now
            };
            _db.FinanceRecords.Add(record);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            if (record.RecordType != "payable" || record.LogisticsProviderId != logisticsProviderId)
                throw new BusinessRuleException("FINANCE_SOURCE_CONFLICT", "财务来源键已被其他往来单占用");
            record.SupplierId = null;
            record.CounterpartyType = "logistics_provider";
            record.Currency = RmbMoneyService.Currency;
        }

        await EnsureLinesAsync(record, lines, cancellationToken);
        await RecalculateAsync(record.Id, cancellationToken);
        return record;
    }

    public async Task RecalculateAsync(long financeRecordId, CancellationToken cancellationToken = default)
    {
        var record = await _db.FinanceRecords.FirstOrDefaultAsync(x => x.Id == financeRecordId, cancellationToken)
            ?? throw new KeyNotFoundException("财务单不存在");
        var amount = await _db.FinanceRecordLines
            .Where(x => x.FinanceRecordId == financeRecordId)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        record.Amount = RmbMoneyService.Round(amount);
        record.Currency = RmbMoneyService.Currency;
        record.Status = record.Amount <= 0m || record.PaidAmount >= record.Amount
            ? "done"
            : record.PaidAmount > 0m ? "partial" : "pending";
        record.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureLinesAsync(
        FinanceRecord record,
        IReadOnlyCollection<FinanceLineInput> lines,
        CancellationToken cancellationToken)
    {
        foreach (var input in lines)
        {
            if (string.IsNullOrWhiteSpace(input.SourceKey))
                throw new BusinessRuleException("FINANCE_LINE_SOURCE_REQUIRED", "财务明细来源键不能为空");
            var existing = await _db.FinanceRecordLines.FirstOrDefaultAsync(
                x => x.SourceKey == input.SourceKey,
                cancellationToken);
            if (existing is not null)
            {
                if (existing.FinanceRecordId != record.Id)
                    throw new BusinessRuleException("FINANCE_LINE_SOURCE_CONFLICT", "财务明细来源键已被其他财务单占用");
                continue;
            }

            _db.FinanceRecordLines.Add(new FinanceRecordLine
            {
                FinanceRecordId = record.Id,
                SourceKey = input.SourceKey,
                LineType = input.LineType,
                SourceType = input.SourceType,
                SourceId = input.SourceId,
                OrderProductId = input.OrderProductId,
                Quantity = RmbMoneyService.Round(input.Quantity),
                UnitPrice = RmbMoneyService.Round(input.UnitPrice),
                Amount = RmbMoneyService.Round(input.Amount),
                PaidAmount = 0m,
                Description = input.Description,
                Status = "pending",
                CreatedAt = DateTime.Now
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
    }
}
