using Futurem.Sourcing.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/bi-reports")]
public class BiReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    public BiReportsController(AppDbContext db) { _db = db; }

    private static DateTime ResolveStart(string? period, DateTime? start)
    {
        var today = DateTime.Today;
        if (start.HasValue) return start.Value.Date;
        return (period ?? "month").ToLowerInvariant() switch
        {
            "today" => today,
            "week" => today.AddDays(-7),
            "quarter" => new DateTime(today.Year, ((today.Month - 1) / 3) * 3 + 1, 1),
            "year" => new DateTime(today.Year, 1, 1),
            "all" => new DateTime(2000, 1, 1),
            _ => new DateTime(today.Year, today.Month, 1)
        };
    }

    private static DateTime ResolveEnd(DateTime? end) => end?.Date ?? DateTime.Today;

    [HttpGet("profit")]
    public async Task<ActionResult<object>> Profit([FromQuery] string period = "month", [FromQuery] DateTime? start = null, [FromQuery] DateTime? end = null)
    {
        var from = ResolveStart(period, start);
        var to = ResolveEnd(end);
        var records = await _db.FinanceRecords.Where(x => (x.RecordDate ?? x.CreatedAt).Date >= from && (x.RecordDate ?? x.CreatedAt).Date <= to).ToListAsync();
        var income = records.Where(x => x.RecordType == "receivable" || x.RecordType == "income").Sum(x => x.Amount);
        var cost = records.Where(x => x.RecordType == "payable" || x.RecordType == "expense").Sum(x => x.Amount);
        var netProfit = income - cost;
        return new
        {
            period,
            start = from,
            end = to,
            income,
            cost,
            receivable = records.Where(x => x.RecordType == "receivable").Sum(x => x.Amount),
            payable = records.Where(x => x.RecordType == "payable").Sum(x => x.Amount),
            expense = records.Where(x => x.RecordType == "expense").Sum(x => x.Amount),
            otherIncome = records.Where(x => x.RecordType == "income").Sum(x => x.Amount),
            netProfit,
            profitRate = income == 0 ? 0 : Math.Round(netProfit / income * 100, 2),
            collected = records.Where(x => x.RecordType == "receivable").Sum(x => x.PaidAmount),
            paid = records.Where(x => x.RecordType == "payable").Sum(x => x.PaidAmount)
        };
    }

    [HttpGet("customer-profit-ranking")]
    public async Task<ActionResult<IEnumerable<object>>> CustomerProfitRanking([FromQuery] string period = "month", [FromQuery] int top = 20)
    {
        var from = ResolveStart(period, null);
        var to = ResolveEnd(null);
        var records = await _db.FinanceRecords.Where(x => (x.RecordDate ?? x.CreatedAt).Date >= from && (x.RecordDate ?? x.CreatedAt).Date <= to).ToListAsync();
        return records.GroupBy(x => x.CustomerId).Where(g => g.Key.HasValue).Select(g =>
        {
            var income = g.Where(x => x.RecordType == "receivable" || x.RecordType == "income").Sum(x => x.Amount);
            var cost = g.Where(x => x.RecordType == "payable" || x.RecordType == "expense").Sum(x => x.Amount);
            var profit = income - cost;
            return new { customerId = g.Key, income, cost, profit, profitRate = income == 0 ? 0 : Math.Round(profit / income * 100, 2), count = g.Count() };
        }).OrderByDescending(x => x.profit).Take(top).Cast<object>().ToList();
    }

    [HttpGet("supplier-purchase-ranking")]
    public async Task<ActionResult<IEnumerable<object>>> SupplierPurchaseRanking([FromQuery] string period = "month", [FromQuery] int top = 20)
    {
        var from = ResolveStart(period, null);
        var to = ResolveEnd(null);
        var records = await _db.FinanceRecords.Where(x => x.RecordType == "payable" && (x.RecordDate ?? x.CreatedAt).Date >= from && (x.RecordDate ?? x.CreatedAt).Date <= to).ToListAsync();
        return records.GroupBy(x => x.SupplierId).Where(g => g.Key.HasValue).Select(g => new
        {
            supplierId = g.Key,
            purchaseAmount = g.Sum(x => x.Amount),
            paidAmount = g.Sum(x => x.PaidAmount),
            balance = g.Sum(x => x.Amount - x.PaidAmount),
            count = g.Count()
        }).OrderByDescending(x => x.purchaseAmount).Take(top).Cast<object>().ToList();
    }

    [HttpGet("product-ranking")]
    public async Task<ActionResult<IEnumerable<object>>> ProductRanking([FromQuery] string documentType = "SO", [FromQuery] int top = 20)
    {
        var lines = await _db.DocumentLines.Where(x => x.DocumentType == documentType && !x.IsDeleted).ToListAsync();
        return lines.GroupBy(x => new { x.ProductId, x.Sku, x.ProductName }).Select(g => new
        {
            g.Key.ProductId,
            g.Key.Sku,
            g.Key.ProductName,
            quantity = g.Sum(x => x.Quantity),
            amount = g.Sum(x => x.Amount),
            cartons = g.Sum(x => x.Cartons),
            cbm = g.Sum(x => x.TotalCbm),
            kg = g.Sum(x => x.TotalGwKg)
        }).OrderByDescending(x => x.amount).Take(top).Cast<object>().ToList();
    }

    [HttpGet("trends")]
    public async Task<ActionResult<IEnumerable<object>>> Trends([FromQuery] string period = "month")
    {
        var from = ResolveStart(period, null);
        var to = ResolveEnd(null);
        var records = await _db.FinanceRecords.Where(x => (x.RecordDate ?? x.CreatedAt).Date >= from && (x.RecordDate ?? x.CreatedAt).Date <= to).ToListAsync();
        return records.GroupBy(x => (x.RecordDate ?? x.CreatedAt).Date).OrderBy(g => g.Key).Select(g =>
        {
            var sales = g.Where(x => x.RecordType == "receivable" || x.RecordType == "income").Sum(x => x.Amount);
            var purchase = g.Where(x => x.RecordType == "payable" || x.RecordType == "expense").Sum(x => x.Amount);
            var profit = sales - purchase;
            return new { date = g.Key, sales, purchase, profit, collected = g.Where(x => x.RecordType == "receivable").Sum(x => x.PaidAmount), paid = g.Where(x => x.RecordType == "payable").Sum(x => x.PaidAmount) };
        }).Cast<object>().ToList();
    }

    [HttpGet("kpi")]
    public async Task<ActionResult<object>> Kpi([FromQuery] string period = "month")
    {
        var from = ResolveStart(period, null);
        var to = ResolveEnd(null);
        var pos = await _db.PurchaseOrders.Where(x => x.CreatedAt.Date >= from && x.CreatedAt.Date <= to).ToListAsync();
        var qcs = await _db.QcOrders.Where(x => x.CreatedAt.Date >= from && x.CreatedAt.Date <= to).ToListAsync();
        var shipments = await _db.Shipments.Where(x => x.CreatedAt.Date >= from && x.CreatedAt.Date <= to).ToListAsync();
        var receivables = await _db.FinanceRecords.Where(x => x.RecordType == "receivable" && (x.RecordDate ?? x.CreatedAt).Date >= from && (x.RecordDate ?? x.CreatedAt).Date <= to).ToListAsync();
        var payables = await _db.FinanceRecords.Where(x => x.RecordType == "payable" && (x.RecordDate ?? x.CreatedAt).Date >= from && (x.RecordDate ?? x.CreatedAt).Date <= to).ToListAsync();
        decimal Rate(decimal a, decimal b) => b == 0 ? 0 : Math.Round(a / b * 100, 2);
        return new
        {
            purchaseCompletionRate = Rate(pos.Count(x => x.Status == "done" || x.Status == "closed"), pos.Count),
            qcPassRate = Rate(qcs.Count(x => x.Result == "passed"), qcs.Count),
            shipmentDoneRate = Rate(shipments.Count(x => x.Status == "arrived" || x.Status == "done"), shipments.Count),
            collectionRate = Rate(receivables.Sum(x => x.PaidAmount), receivables.Sum(x => x.Amount)),
            paymentRate = Rate(payables.Sum(x => x.PaidAmount), payables.Sum(x => x.Amount))
        };
    }
}
