using Futurem.Sourcing.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/global-search")]
public class GlobalSearchController : ControllerBase
{
    private readonly AppDbContext _db;
    public GlobalSearchController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<ActionResult<object>> Search([FromQuery] string keyword, [FromQuery] int top = 10)
    {
        keyword = (keyword ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(keyword)) return new { keyword, total = 0, items = Array.Empty<object>() };
        var like = $"%{keyword}%";
        var items = new List<object>();

        items.AddRange(await _db.Products.Where(x => EF.Functions.Like(x.Sku, like) || EF.Functions.Like(x.NameCn, like) || EF.Functions.Like(x.NameEn, like) || EF.Functions.Like(x.Barcode, like)).OrderByDescending(x => x.Id).Take(top).Select(x => new { module = "products", type = "商品", id = x.Id, no = x.Sku, title = x.NameCn + " " + x.NameEn, route = "/products" }).ToListAsync());
        items.AddRange(await _db.Customers.Where(x => EF.Functions.Like(x.Code, like) || EF.Functions.Like(x.Name, like) || EF.Functions.Like(x.ContactName, like) || EF.Functions.Like(x.Email, like)).OrderByDescending(x => x.Id).Take(top).Select(x => new { module = "customers", type = "客户", id = x.Id, no = x.Code, title = x.Name, route = "/customers" }).ToListAsync());
        items.AddRange(await _db.Suppliers.Where(x => EF.Functions.Like(x.Code, like) || EF.Functions.Like(x.Name, like) || EF.Functions.Like(x.ContactName, like) || EF.Functions.Like(x.Email, like)).OrderByDescending(x => x.Id).Take(top).Select(x => new { module = "suppliers", type = "供应商", id = x.Id, no = x.Code, title = x.Name, route = "/suppliers" }).ToListAsync());
        items.AddRange(await _db.Rfqs.Where(x => EF.Functions.Like(x.No, like) || EF.Functions.Like(x.Status, like)).OrderByDescending(x => x.Id).Take(top).Select(x => new { module = "rfqs", type = "RFQ", id = x.Id, no = x.No, title = x.Status, route = "/rfqs" }).ToListAsync());
        items.AddRange(await _db.CustomerOrders.Where(x => EF.Functions.Like(x.No, like) || EF.Functions.Like(x.Status, like)).OrderByDescending(x => x.Id).Take(top).Select(x => new { module = "customer-orders", type = "CO", id = x.Id, no = x.No, title = x.Status, route = "/customer-orders" }).ToListAsync());
        items.AddRange(await _db.PurchaseOrders.Where(x => EF.Functions.Like(x.No, like) || EF.Functions.Like(x.Status, like)).OrderByDescending(x => x.Id).Take(top).Select(x => new { module = "purchase-orders", type = "PO", id = x.Id, no = x.No, title = x.Status, route = "/purchase-orders" }).ToListAsync());
        items.AddRange(await _db.SummaryOrders.Where(x => EF.Functions.Like(x.No, like) || EF.Functions.Like(x.Status, like)).OrderByDescending(x => x.Id).Take(top).Select(x => new { module = "so-orders", type = "SO", id = x.Id, no = x.No, title = x.Status, route = "/so-orders" }).ToListAsync());
        items.AddRange(await _db.ContainerLoads.Where(x => EF.Functions.Like(x.No, like) || EF.Functions.Like(x.ContainerNo, like) || EF.Functions.Like(x.Status, like)).OrderByDescending(x => x.Id).Take(top).Select(x => new { module = "container-loads", type = "装柜", id = x.Id, no = x.No, title = x.ContainerNo + " " + x.Status, route = "/container-loads" }).ToListAsync());
        items.AddRange(await _db.Shipments.Where(x => EF.Functions.Like(x.No, like) || EF.Functions.Like(x.Carrier, like) || EF.Functions.Like(x.BillOfLadingNo, like) || EF.Functions.Like(x.Status, like)).OrderByDescending(x => x.Id).Take(top).Select(x => new { module = "shipments", type = "出运", id = x.Id, no = x.No, title = x.Carrier + " " + x.BillOfLadingNo, route = "/shipments" }).ToListAsync());
        items.AddRange(await _db.FinanceRecords.Where(x => EF.Functions.Like(x.No, like) || EF.Functions.Like(x.RecordType, like) || EF.Functions.Like(x.TargetType, like)).OrderByDescending(x => x.Id).Take(top).Select(x => new { module = "finance-records", type = "财务", id = x.Id, no = x.No, title = x.RecordType + " " + x.Amount, route = "/finance-records" }).ToListAsync());

        return new { keyword, total = items.Count, items = items.Take(top * 10) };
    }
}
