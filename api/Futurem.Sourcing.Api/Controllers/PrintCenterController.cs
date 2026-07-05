using System.Text;
using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/print-center")]
public class PrintCenterController : ControllerBase
{
    private readonly AppDbContext _db;
    public PrintCenterController(AppDbContext db) { _db = db; }

    [HttpGet("templates")]
    public async Task<ActionResult<IEnumerable<PrintTemplate>>> Templates([FromQuery] string? documentType = null, [FromQuery] string? language = null)
    {
        var query = _db.PrintTemplates.AsQueryable();
        if (!string.IsNullOrWhiteSpace(documentType)) query = query.Where(x => x.DocumentType == documentType);
        if (!string.IsNullOrWhiteSpace(language)) query = query.Where(x => x.Language == language);
        return await query.OrderBy(x => x.DocumentType).ThenByDescending(x => x.IsDefault).ThenBy(x => x.Name).ToListAsync();
    }

    [HttpPost("templates")]
    public async Task<ActionResult<PrintTemplate>> CreateTemplate(PrintTemplate input)
    {
        input.Id = 0;
        input.CreatedAt = DateTime.Now;
        _db.PrintTemplates.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("templates/{id:long}")]
    public async Task<ActionResult<PrintTemplate>> UpdateTemplate(long id, PrintTemplate input)
    {
        var entity = await _db.PrintTemplates.FindAsync(id);
        if (entity == null) return NotFound();
        entity.Code = input.Code;
        entity.Name = input.Name;
        entity.DocumentType = input.DocumentType;
        entity.Language = input.Language;
        entity.PaperSize = input.PaperSize;
        entity.Body = input.Body;
        entity.IsDefault = input.IsDefault;
        entity.Status = input.Status;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("templates/{id:long}")]
    public async Task<IActionResult> DeleteTemplate(long id)
    {
        var entity = await _db.PrintTemplates.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpGet("documents")]
    public ActionResult<IEnumerable<object>> Documents()
    {
        return new object[]
        {
            new { type = "QUOTATION", name = "Quotation 报价单" },
            new { type = "PI", name = "Proforma Invoice 形式发票" },
            new { type = "PO", name = "Purchase Order 采购单" },
            new { type = "SO", name = "Sales Order 销售单" },
            new { type = "INVOICE", name = "Commercial Invoice 商业发票" },
            new { type = "PACKING", name = "Packing List 装箱单" },
            new { type = "QC", name = "QC Report 质检报告" },
            new { type = "RECEIVING", name = "Receiving 收货单" },
            new { type = "CONTAINER", name = "Container Loading List 装柜单" },
            new { type = "SHIPMENT", name = "Shipment 出运单" },
            new { type = "PAYMENT", name = "Payment Voucher 收付款单" }
        };
    }

    [HttpGet("preview")]
    public async Task<ActionResult<object>> Preview([FromQuery] string documentType, [FromQuery] long id, [FromQuery] string language = "en")
    {
        var data = await ResolveDocument(documentType, id);
        var template = await _db.PrintTemplates.FirstOrDefaultAsync(x => x.DocumentType == documentType && x.Language == language && x.IsDefault)
            ?? await _db.PrintTemplates.FirstOrDefaultAsync(x => x.DocumentType == documentType && x.Language == language)
            ?? DefaultTemplate(documentType, language);
        var html = RenderTemplate(template.Body, data);
        return new { documentType, id, language, paperSize = template.PaperSize, html };
    }

    [HttpGet("html")]
    public async Task<IActionResult> Html([FromQuery] string documentType, [FromQuery] long id, [FromQuery] string language = "en")
    {
        var preview = await Preview(documentType, id, language);
        var value = (preview.Result as ObjectResult)?.Value ?? preview.Value;
        var html = value?.GetType().GetProperty("html")?.GetValue(value)?.ToString() ?? string.Empty;
        return Content(html, "text/html", Encoding.UTF8);
    }

    [HttpPost("seed")]
    public async Task<ActionResult<object>> Seed()
    {
        var docs = new[] { "QUOTATION", "PI", "PO", "SO", "INVOICE", "PACKING", "QC", "RECEIVING", "CONTAINER", "SHIPMENT", "PAYMENT" };
        foreach (var doc in docs)
        {
            var code = $"{doc}_EN_DEFAULT";
            if (!await _db.PrintTemplates.AnyAsync(x => x.Code == code))
            {
                _db.PrintTemplates.Add(new PrintTemplate { Code = code, Name = $"{doc} Default", DocumentType = doc, Language = "en", PaperSize = "A4", Body = DefaultBody(doc), IsDefault = true, Status = "active", CreatedAt = DateTime.Now });
            }
        }
        await _db.SaveChangesAsync();
        return new { templates = await _db.PrintTemplates.CountAsync() };
    }

    private async Task<Dictionary<string, string>> ResolveDocument(string documentType, long id)
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["company"] = "FUTUREM",
            ["date"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["documentType"] = documentType,
            ["id"] = id.ToString()
        };
        if (documentType == "PO")
        {
            var po = await _db.PurchaseOrders.FindAsync(id);
            if (po != null) { data["no"] = po.No; data["currency"] = po.Currency; data["status"] = po.Status; data["amount"] = (await _db.DocumentLines.Where(x => x.DocumentType == "PO" && x.DocumentId == po.Id).SumAsync(x => x.Amount)).ToString("0.##"); data["date"] = po.OrderDate?.ToString("yyyy-MM-dd") ?? data["date"]; }
        }
        else if (documentType == "SO" || documentType == "PI" || documentType == "INVOICE")
        {
            var so = await _db.SummaryOrders.FindAsync(id);
            if (so != null) { data["no"] = so.No; data["currency"] = so.Currency; data["status"] = so.Status; data["amount"] = so.ReceivableAmount.ToString("0.##"); data["date"] = so.OrderDate?.ToString("yyyy-MM-dd") ?? data["date"]; }
        }
        else if (documentType == "CONTAINER")
        {
            var c = await _db.ContainerLoads.FindAsync(id);
            if (c != null) { data["no"] = c.No; data["status"] = c.Status; data["containerType"] = c.ContainerType; data["cbm"] = c.TotalCbm.ToString("0.##"); data["kg"] = c.TotalGwKg.ToString("0.##"); data["date"] = c.LoadDate?.ToString("yyyy-MM-dd") ?? data["date"]; }
        }
        else if (documentType == "SHIPMENT")
        {
            var s = await _db.Shipments.FindAsync(id);
            if (s != null) { data["no"] = s.No; data["status"] = s.Status; data["carrier"] = s.Carrier ?? string.Empty; data["mode"] = s.ShipmentMode; data["date"] = s.Etd?.ToString("yyyy-MM-dd") ?? data["date"]; data["eta"] = s.Eta?.ToString("yyyy-MM-dd") ?? string.Empty; }
        }
        else if (documentType == "PAYMENT")
        {
            var p = await _db.Payments.FindAsync(id);
            if (p != null) { data["no"] = p.No; data["currency"] = p.Currency; data["amount"] = p.Amount.ToString("0.##"); data["date"] = p.PaymentDate?.ToString("yyyy-MM-dd") ?? data["date"]; data["method"] = p.PaymentMethod; data["direction"] = p.Direction; }
        }
        if (!data.ContainsKey("no")) data["no"] = $"{documentType}-{id}";
        if (!data.ContainsKey("amount")) data["amount"] = "0";
        if (!data.ContainsKey("status")) data["status"] = "draft";
        return data;
    }

    private static PrintTemplate DefaultTemplate(string documentType, string language) => new() { DocumentType = documentType, Language = language, PaperSize = "A4", Body = DefaultBody(documentType) };

    private static string DefaultBody(string documentType) => "<html><body style='font-family:Arial;padding:32px'><h1>{{documentType}}</h1><h2>{{company}}</h2><p>No: {{no}}</p><p>Date: {{date}}</p><p>Status: {{status}}</p><p>Amount: {{amount}} {{currency}}</p><hr/><p>Generated by FUTUREM Enterprise</p></body></html>";

    private static string RenderTemplate(string body, Dictionary<string, string> data)
    {
        var html = body;
        foreach (var kv in data) html = html.Replace("{{" + kv.Key + "}}", kv.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        return html;
    }
}
