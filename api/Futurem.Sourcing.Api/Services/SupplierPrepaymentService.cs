using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public class SupplierPrepaymentService
{
    private readonly AppDbContext _db;

    public SupplierPrepaymentService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<decimal> ApplyAvailableAsync(FinanceRecord finance)
    {
        if (!finance.SupplierId.HasValue) return 0m;
        var outstanding = FinanceBalanceService.Outstanding(finance);
        if (outstanding <= 0m) return 0m;

        var prepayments = await _db.SupplierPrepayments
            .Where(x => x.SupplierId == finance.SupplierId.Value &&
                        x.Currency == finance.Currency &&
                        x.AvailableAmount > 0m &&
                        (x.Status == "available" || x.Status == "partially_used"))
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync();

        var applied = 0m;
        foreach (var prepayment in prepayments)
        {
            outstanding = FinanceBalanceService.Outstanding(finance);
            if (outstanding <= 0m) break;

            var amount = FinanceBalanceService.Round2(Math.Min(outstanding, prepayment.AvailableAmount));
            if (amount <= 0m) continue;

            prepayment.AvailableAmount = FinanceBalanceService.Round2(prepayment.AvailableAmount - amount);
            RefreshPrepaymentStatus(prepayment);
            finance.PrepaymentAppliedAmount = FinanceBalanceService.Round2(finance.PrepaymentAppliedAmount + amount);
            _db.SupplierPrepaymentUsages.Add(new SupplierPrepaymentUsage
            {
                SupplierPrepaymentId = prepayment.Id,
                FinanceRecordId = finance.Id,
                Amount = amount,
                UsageType = "apply",
                CreatedAt = DateTime.Now,
                Remark = $"自动抵扣应付 {finance.No}"
            });
            applied += amount;
        }

        FinanceBalanceService.RefreshStatus(finance);
        finance.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return FinanceBalanceService.Round2(applied);
    }

    public async Task<decimal> ReleaseApplicationsAsync(FinanceRecord finance, decimal maxCreditToKeep)
    {
        maxCreditToKeep = Math.Max(0m, FinanceBalanceService.Round2(maxCreditToKeep));
        var current = FinanceBalanceService.Round2(finance.PrepaymentAppliedAmount);
        var releaseNeeded = FinanceBalanceService.Round2(Math.Max(0m, current - maxCreditToKeep));
        if (releaseNeeded <= 0m) return 0m;

        var usages = await _db.SupplierPrepaymentUsages
            .Where(x => x.FinanceRecordId == finance.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        var netByPrepayment = usages
            .GroupBy(x => x.SupplierPrepaymentId)
            .Select(g => new
            {
                PrepaymentId = g.Key,
                NetApplied = g.Where(x => x.UsageType == "apply").Sum(x => x.Amount) -
                             g.Where(x => x.UsageType == "release").Sum(x => x.Amount),
                LastAt = g.Max(x => x.CreatedAt)
            })
            .Where(x => x.NetApplied > 0m)
            .OrderByDescending(x => x.LastAt)
            .ToList();

        var released = 0m;
        foreach (var item in netByPrepayment)
        {
            if (releaseNeeded <= 0m) break;
            var prepayment = await _db.SupplierPrepayments.FindAsync(item.PrepaymentId);
            if (prepayment == null) continue;

            var amount = FinanceBalanceService.Round2(Math.Min(releaseNeeded, item.NetApplied));
            prepayment.AvailableAmount = FinanceBalanceService.Round2(prepayment.AvailableAmount + amount);
            RefreshPrepaymentStatus(prepayment);
            finance.PrepaymentAppliedAmount = FinanceBalanceService.Round2(finance.PrepaymentAppliedAmount - amount);
            _db.SupplierPrepaymentUsages.Add(new SupplierPrepaymentUsage
            {
                SupplierPrepaymentId = prepayment.Id,
                FinanceRecordId = finance.Id,
                Amount = amount,
                UsageType = "release",
                CreatedAt = DateTime.Now,
                Remark = $"释放应付 {finance.No} 的预付款抵扣"
            });
            releaseNeeded = FinanceBalanceService.Round2(releaseNeeded - amount);
            released += amount;
        }

        FinanceBalanceService.RefreshStatus(finance);
        finance.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return FinanceBalanceService.Round2(released);
    }

    public async Task<SupplierPrepayment?> UpsertOverpaymentAsync(FinanceRecord finance, decimal desiredTransfer)
    {
        desiredTransfer = Math.Max(0m, FinanceBalanceService.Round2(desiredTransfer));
        var existing = await _db.SupplierPrepayments.FirstOrDefaultAsync(x =>
            x.SourceType == "SHIPMENT_EXPENSE_OVERPAYMENT" &&
            x.SourceFinanceRecordId == finance.Id);

        if (existing == null)
        {
            if (desiredTransfer <= 0m)
            {
                finance.OverpaymentTransferredAmount = 0m;
                FinanceBalanceService.RefreshStatus(finance);
                return null;
            }
            if (!finance.SupplierId.HasValue)
                throw new InvalidOperationException("供应商应付缺少供应商，无法生成预付款");

            existing = new SupplierPrepayment
            {
                No = NumberService.NewNo("ADV"),
                SupplierId = finance.SupplierId.Value,
                Currency = finance.Currency,
                OriginalAmount = desiredTransfer,
                AvailableAmount = desiredTransfer,
                SourceType = "SHIPMENT_EXPENSE_OVERPAYMENT",
                SourceId = finance.ShipmentExpenseId ?? finance.TargetId,
                SourceFinanceRecordId = finance.Id,
                Status = "available",
                CreatedAt = DateTime.Now,
                Remark = $"应付 {finance.No} 超付转预付款"
            };
            _db.SupplierPrepayments.Add(existing);
        }
        else
        {
            var used = Math.Max(0m, FinanceBalanceService.Round2(existing.OriginalAmount - existing.AvailableAmount));
            var actualOriginal = Math.Max(used, desiredTransfer);
            var delta = FinanceBalanceService.Round2(actualOriginal - existing.OriginalAmount);
            existing.OriginalAmount = actualOriginal;
            existing.AvailableAmount = Math.Max(0m, FinanceBalanceService.Round2(existing.AvailableAmount + delta));
            existing.SupplierId = finance.SupplierId ?? existing.SupplierId;
            existing.Currency = finance.Currency;
            existing.UpdatedAt = DateTime.Now;
            RefreshPrepaymentStatus(existing);
        }

        finance.OverpaymentTransferredAmount = existing.OriginalAmount;
        FinanceBalanceService.RefreshStatus(finance);
        finance.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return existing;
    }

    private static void RefreshPrepaymentStatus(SupplierPrepayment prepayment)
    {
        prepayment.AvailableAmount = Math.Max(0m, FinanceBalanceService.Round2(prepayment.AvailableAmount));
        prepayment.OriginalAmount = Math.Max(0m, FinanceBalanceService.Round2(prepayment.OriginalAmount));
        prepayment.Status = prepayment.OriginalAmount <= 0m
            ? "cancelled"
            : prepayment.AvailableAmount <= 0m
                ? "used"
                : prepayment.AvailableAmount < prepayment.OriginalAmount
                    ? "partially_used"
                    : "available";
    }
}
