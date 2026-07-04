using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public static class FinanceAutoService
{
    public static async Task<FinanceRecord> EnsureReceivableAsync(AppDbContext db, string targetType, long targetId, long? customerId, string currency, decimal amount, string? remark = null)
    {
        var existing = await db.FinanceRecords.FirstOrDefaultAsync(x => x.RecordType == "receivable" && x.TargetType == targetType && x.TargetId == targetId);
        if (existing != null)
        {
            existing.CustomerId = customerId;
            existing.Currency = currency;
            existing.Amount = amount;
            existing.Status = existing.PaidAmount <= 0 ? "pending" : existing.PaidAmount < existing.Amount ? "partial" : "done";
            existing.Remark = remark ?? existing.Remark;
            existing.UpdatedAt = DateTime.Now;
            return existing;
        }

        var record = new FinanceRecord
        {
            No = NumberService.NewNo("AR"),
            RecordType = "receivable",
            TargetType = targetType,
            TargetId = targetId,
            CustomerId = customerId,
            Currency = currency,
            Amount = amount,
            PaidAmount = 0,
            RecordDate = DateTime.Today,
            Status = "pending",
            Remark = remark,
            CreatedAt = DateTime.Now
        };
        db.FinanceRecords.Add(record);
        return record;
    }

    public static async Task<FinanceRecord> EnsurePayableAsync(AppDbContext db, string targetType, long targetId, long? supplierId, long? customerId, string currency, decimal amount, string? remark = null)
    {
        var existing = await db.FinanceRecords.FirstOrDefaultAsync(x => x.RecordType == "payable" && x.TargetType == targetType && x.TargetId == targetId);
        if (existing != null)
        {
            existing.SupplierId = supplierId;
            existing.CustomerId = customerId;
            existing.Currency = currency;
            existing.Amount = amount;
            existing.Status = existing.PaidAmount <= 0 ? "pending" : existing.PaidAmount < existing.Amount ? "partial" : "done";
            existing.Remark = remark ?? existing.Remark;
            existing.UpdatedAt = DateTime.Now;
            return existing;
        }

        var record = new FinanceRecord
        {
            No = NumberService.NewNo("AP"),
            RecordType = "payable",
            TargetType = targetType,
            TargetId = targetId,
            SupplierId = supplierId,
            CustomerId = customerId,
            Currency = currency,
            Amount = amount,
            PaidAmount = 0,
            RecordDate = DateTime.Today,
            Status = "pending",
            Remark = remark,
            CreatedAt = DateTime.Now
        };
        db.FinanceRecords.Add(record);
        return record;
    }

    public static async Task<decimal> SumDocumentAmountAsync(AppDbContext db, string documentType, long documentId)
    {
        return await db.DocumentLines.Where(x => x.DocumentType == documentType && x.DocumentId == documentId).SumAsync(x => x.Amount);
    }
}
