using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/finance-records")]
public class FinanceRecordsController : ControllerBase
{
    private readonly AppDbContext _db;
    public FinanceRecordsController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FinanceRecord>>> List([FromQuery] string? recordType, [FromQuery] string? targetType, [FromQuery] long? customerId, [FromQuery] long? supplierId, [FromQuery] string? status)
    {
        var query = _db.FinanceRecords.AsQueryable();
        if (!string.IsNullOrWhiteSpace(recordType)) query = query.Where(x => x.RecordType == recordType);
        if (!string.IsNullOrWhiteSpace(targetType)) query = query.Where(x => x.TargetType == targetType);
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return await query.OrderByDescending(x => x.Id).Take(300).ToListAsync();
    }

    [HttpGet("summary")]
    public async Task<ActionResult<object>> Summary([FromQuery] string? recordType, [FromQuery] string? currency)
    {
        var query = _db.FinanceRecords.AsQueryable();
        if (!string.IsNullOrWhiteSpace(recordType)) query = query.Where(x => x.RecordType == recordType);
        if (!string.IsNullOrWhiteSpace(currency)) query = query.Where(x => x.Currency == currency);
        var records = await query.ToListAsync();
        return new
        {
            totalAmount = records.Sum(x => x.Amount),
            paidAmount = records.Sum(x => x.PaidAmount),
            balanceAmount = records.Sum(x => x.Amount - x.PaidAmount),
            receivableBalance = records.Where(x => x.RecordType == "receivable").Sum(x => x.Amount - x.PaidAmount),
            payableBalance = records.Where(x => x.RecordType == "payable").Sum(x => x.Amount - x.PaidAmount),
            pendingCount = records.Count(x => x.Status == "pending"),
            partialCount = records.Count(x => x.Status == "partial"),
            doneCount = records.Count(x => x.Status == "done")
        };
    }

    [HttpGet("profit-summary")]
    public async Task<ActionResult<object>> ProfitSummary([FromQuery] long? customerId = null, [FromQuery] string? currency = null)
    {
        var query = _db.FinanceRecords.AsQueryable();
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        if (!string.IsNullOrWhiteSpace(currency)) query = query.Where(x => x.Currency == currency);
        var records = await query.ToListAsync();

        var soIncome = records.Where(x => x.RecordType == "receivable" && x.TargetType == "SO").Sum(x => x.Amount);
        var poCost = records.Where(x => x.RecordType == "payable" && x.TargetType == "PO").Sum(x => x.Amount);
        var expense = records.Where(x => x.RecordType == "expense").Sum(x => x.Amount);
        var otherIncome = records.Where(x => x.RecordType == "income").Sum(x => x.Amount);
        var grossProfit = soIncome - poCost;
        var netProfit = soIncome + otherIncome - poCost - expense;
        var profitRate = soIncome == 0 ? 0 : Math.Round(netProfit / soIncome * 100, 2);

        return new
        {
            soIncome,
            poCost,
            expense,
            otherIncome,
            grossProfit,
            netProfit,
            profitRate,
            receivableCollected = records.Where(x => x.RecordType == "receivable" && x.TargetType == "SO").Sum(x => x.PaidAmount),
            payablePaid = records.Where(x => x.RecordType == "payable" && x.TargetType == "PO").Sum(x => x.PaidAmount)
        };
    }

    [HttpGet("aging")]
    public async Task<ActionResult<object>> Aging([FromQuery] string recordType = "receivable", [FromQuery] long? customerId = null, [FromQuery] long? supplierId = null)
    {
        var today = DateTime.Today;
        var query = _db.FinanceRecords.Where(x => x.RecordType == recordType && x.Status != "done");
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value);
        var records = await query.ToListAsync();
        decimal Balance(FinanceRecord x) => x.Amount - x.PaidAmount;
        int Days(FinanceRecord x) => (today - (x.RecordDate ?? x.CreatedAt).Date).Days;
        return new
        {
            current = records.Where(x => Days(x) <= 30).Sum(Balance),
            days31To60 = records.Where(x => Days(x) >= 31 && Days(x) <= 60).Sum(Balance),
            days61To90 = records.Where(x => Days(x) >= 61 && Days(x) <= 90).Sum(Balance),
            over90 = records.Where(x => Days(x) > 90).Sum(Balance),
            total = records.Sum(Balance),
            count = records.Count
        };
    }

    [HttpGet("partner-balances")]
    public async Task<ActionResult<IEnumerable<object>>> PartnerBalances([FromQuery] string recordType = "receivable")
    {
        var records = await _db.FinanceRecords.Where(x => x.RecordType == recordType && x.Status != "done").ToListAsync();
        if (recordType == "payable")
        {
            return records.GroupBy(x => x.SupplierId).Select(g => new
            {
                supplierId = g.Key,
                amount = g.Sum(x => x.Amount),
                paidAmount = g.Sum(x => x.PaidAmount),
                balanceAmount = g.Sum(x => x.Amount - x.PaidAmount),
                count = g.Count()
            }).OrderByDescending(x => x.balanceAmount).Cast<object>().ToList();
        }

        return records.GroupBy(x => x.CustomerId).Select(g => new
        {
            customerId = g.Key,
            amount = g.Sum(x => x.Amount),
            paidAmount = g.Sum(x => x.PaidAmount),
            balanceAmount = g.Sum(x => x.Amount - x.PaidAmount),
            count = g.Count()
        }).OrderByDescending(x => x.balanceAmount).Cast<object>().ToList();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<FinanceRecord>> Get(long id)
    {
        var entity = await _db.FinanceRecords.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<FinanceRecord>> Create(FinanceRecord input)
    {
        input.Id = 0;
        input.No = string.IsNullOrWhiteSpace(input.No) ? NumberService.NewNo("FIN") : input.No;
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "pending" : input.Status;
        input.CreatedAt = DateTime.Now;
        _db.FinanceRecords.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<FinanceRecord>> Update(long id, FinanceRecord input)
    {
        var entity = await _db.FinanceRecords.FindAsync(id);
        if (entity == null) return NotFound();
        entity.RecordType = input.RecordType;
        entity.TargetType = input.TargetType;
        entity.TargetId = input.TargetId;
        entity.CustomerId = input.CustomerId;
        entity.SupplierId = input.SupplierId;
        entity.Currency = input.Currency;
        entity.Amount = input.Amount;
        entity.PaidAmount = input.PaidAmount;
        entity.RecordDate = input.RecordDate;
        entity.Status = input.Status;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.FinanceRecords.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
