using System.Text;
using Futurem.Sourcing.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/excel-center")]
public class ExcelCenterController : ControllerBase
{
    private readonly AppDbContext _db;
    public ExcelCenterController(AppDbContext db) { _db = db; }

    [HttpGet("modules")]
    public ActionResult<IEnumerable<object>> Modules()
    {
        return new object[]
        {
            new { code = "customers", name = "客户", template = new[] { "code", "name", "country", "contact", "email", "phone" } },
            new { code = "suppliers", name = "供应商", template = new[] { "code", "name", "country", "contact", "email", "phone" } },
            new { code = "products", name = "商品", template = new[] { "sku", "nameCn", "nameEn", "barcode", "unit", "cartonQty" } },
            new { code = "rfqs", name = "RFQ", template = new[] { "no", "customerId", "productId", "quantity", "targetPrice", "currency" } },
            new { code = "purchase-orders", name = "采购订单", template = new[] { "no", "supplierId", "customerId", "orderDate", "currency", "totalAmount" } },
            new { code = "so-orders", name = "SO汇总", template = new[] { "no", "customerId", "orderDate", "currency", "receivableAmount" } },
            new { code = "finance-records", name = "财务记录", template = new[] { "no", "recordType", "targetType", "targetId", "currency", "amount" } },
            new { code = "payments", name = "收付款", template = new[] { "no", "direction", "currency", "amount", "paymentDate", "paymentMethod" } }
        };
    }

    [HttpGet("template/{module}")]
    public IActionResult Template(string module)
    {
        var headers = Headers(module);
        var csv = string.Join(',', headers) + "\n";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"{module}_template.csv");
    }

    [HttpGet("export/{module}")]
    public async Task<IActionResult> Export(string module)
    {
        var csv = module switch
        {
            "customers" => ToCsv(Headers(module), (await _db.Customers.OrderByDescending(x => x.Id).Take(5000).ToListAsync()).Select(x => new[] { x.Code, x.Name, x.Country, x.ContactName, x.Email, x.Phone })),
            "suppliers" => ToCsv(Headers(module), (await _db.Suppliers.OrderByDescending(x => x.Id).Take(5000).ToListAsync()).Select(x => new[] { x.Code, x.Name, "", x.ContactName, x.Email, x.Phone })),
            "products" => ToCsv(Headers(module), (await _db.Products.OrderByDescending(x => x.Id).Take(5000).ToListAsync()).Select(x => new[] { x.Sku, x.NameCn, x.NameEn, x.Barcode, x.Unit, "" })),
            "purchase-orders" => await ExportPurchaseOrders(),
            "so-orders" => ToCsv(Headers(module), (await _db.SummaryOrders.OrderByDescending(x => x.Id).Take(5000).ToListAsync()).Select(x => new[] { x.No, x.CustomerId.ToString(), x.OrderDate?.ToString("yyyy-MM-dd") ?? "", x.Currency, x.ReceivableAmount.ToString("0.##") })),
            "finance-records" => ToCsv(Headers(module), (await _db.FinanceRecords.OrderByDescending(x => x.Id).Take(5000).ToListAsync()).Select(x => new[] { x.No, x.RecordType, x.TargetType, x.TargetId.ToString(), x.Currency, x.Amount.ToString("0.##") })),
            "payments" => ToCsv(Headers(module), (await _db.Payments.OrderByDescending(x => x.Id).Take(5000).ToListAsync()).Select(x => new[] { x.No, x.Direction, x.Currency, x.Amount.ToString("0.##"), x.PaymentDate?.ToString("yyyy-MM-dd") ?? "", x.PaymentMethod })),
            _ => ToCsv(Headers(module), Array.Empty<string[]>())
        };
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"{module}_{DateTime.Now:yyyyMMddHHmmss}.csv");
    }

    private async Task<string> ExportPurchaseOrders()
    {
        var orders = await _db.PurchaseOrders.OrderByDescending(x => x.Id).Take(5000).ToListAsync();
        var orderIds = orders.Select(x => x.Id).ToList();
        var totals = await _db.DocumentLines
            .Where(x => x.DocumentType == "PO" && orderIds.Contains(x.DocumentId))
            .GroupBy(x => x.DocumentId)
            .Select(x => new { DocumentId = x.Key, Total = x.Sum(line => line.Amount) })
            .ToDictionaryAsync(x => x.DocumentId, x => x.Total);

        return ToCsv(Headers("purchase-orders"), orders.Select(x => new[]
        {
            x.No,
            x.SupplierId.ToString(),
            x.CustomerId?.ToString() ?? "",
            x.OrderDate?.ToString("yyyy-MM-dd") ?? "",
            x.Currency,
            totals.GetValueOrDefault(x.Id).ToString("0.##")
        }));
    }

    [HttpPost("import/{module}")]
    public async Task<ActionResult<object>> Import(string module, IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded");
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        var text = await reader.ReadToEndAsync();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
        return new { module, rows = lines.Count, imported = 0, message = "CSV parsed. Import mapping will be enabled per module in the next step." };
    }

    private static string[] Headers(string module) => module switch
    {
        "customers" => new[] { "code", "name", "country", "contact", "email", "phone" },
        "suppliers" => new[] { "code", "name", "country", "contact", "email", "phone" },
        "products" => new[] { "sku", "nameCn", "nameEn", "barcode", "unit", "cartonQty" },
        "purchase-orders" => new[] { "no", "supplierId", "customerId", "orderDate", "currency", "totalAmount" },
        "so-orders" => new[] { "no", "customerId", "orderDate", "currency", "receivableAmount" },
        "finance-records" => new[] { "no", "recordType", "targetType", "targetId", "currency", "amount" },
        "payments" => new[] { "no", "direction", "currency", "amount", "paymentDate", "paymentMethod" },
        _ => new[] { "no", "name", "remark" }
    };

    private static string ToCsv(IEnumerable<string> headers, IEnumerable<string?[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', headers.Select(Escape)));
        foreach (var row in rows) sb.AppendLine(string.Join(',', row.Select(Escape)));
        return sb.ToString();
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        value = value.Replace("\"", "\"\"");
        return $"\"{value}\"";
    }
}
