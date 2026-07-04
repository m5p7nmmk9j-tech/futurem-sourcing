using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _db;
    public NotificationsController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Notification>>> List([FromQuery] string? status = null, [FromQuery] string? level = null)
    {
        var query = _db.Notifications.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(level)) query = query.Where(x => x.Level == level);
        return await query.OrderByDescending(x => x.Id).Take(200).ToListAsync();
    }

    [HttpGet("summary")]
    public async Task<ActionResult<object>> Summary()
    {
        var unread = await _db.Notifications.Where(x => x.Status == "unread").ToListAsync();
        return new { unread = unread.Count, danger = unread.Count(x => x.Level == "danger"), warning = unread.Count(x => x.Level == "warning"), info = unread.Count(x => x.Level == "info") };
    }

    [HttpPost]
    public async Task<ActionResult<Notification>> Create(Notification input)
    {
        input.Id = 0;
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "unread" : input.Status;
        input.Level = string.IsNullOrWhiteSpace(input.Level) ? "info" : input.Level;
        input.CreatedAt = DateTime.Now;
        _db.Notifications.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPost("generate-from-warnings")]
    public async Task<ActionResult<object>> GenerateFromWarnings()
    {
        var today = DateTime.Today;
        var soon = today.AddDays(7);
        var created = 0;
        async Task AddIfMissing(string sourceType, long? sourceId, string level, string title, string message)
        {
            var exists = await _db.Notifications.AnyAsync(x => x.SourceType == sourceType && x.SourceId == sourceId && x.Status == "unread" && x.Title == title);
            if (exists) return;
            _db.Notifications.Add(new Notification { SourceType = sourceType, SourceId = sourceId, Level = level, Title = title, Message = message, Status = "unread", CreatedAt = DateTime.Now });
            created++;
        }

        var pos = await _db.PurchaseOrders.Where(x => x.Status != "done" && x.Status != "closed" && x.ExpectedDeliveryDate.HasValue && x.ExpectedDeliveryDate.Value.Date <= soon).ToListAsync();
        foreach (var po in pos) await AddIfMissing("PO", po.Id, po.ExpectedDeliveryDate!.Value.Date < today ? "danger" : "warning", $"PO交期提醒 {po.No}", po.ExpectedDeliveryDate.Value.Date < today ? "采购订单已超过预计交期" : "采购订单即将到交期");

        var qcs = await _db.QcOrders.Where(x => x.Result == "failed" || (x.Status != "done" && x.Result != "passed")).ToListAsync();
        foreach (var qc in qcs) await AddIfMissing("QC", qc.Id, qc.Result == "failed" ? "danger" : "warning", $"QC提醒 {qc.No}", qc.Result == "failed" ? "QC异常未处理" : "QC待完成");

        var shipments = await _db.Shipments.Where(x => x.Status != "arrived" && x.Status != "done" && ((x.Etd.HasValue && x.Etd.Value.Date <= soon) || (x.Eta.HasValue && x.Eta.Value.Date <= soon))).ToListAsync();
        foreach (var s in shipments) await AddIfMissing("SHIPMENT", s.Id, (s.Eta.HasValue && s.Eta.Value.Date < today) ? "danger" : "warning", $"出运提醒 {s.No}", "ETD/ETA 即将到期或已超期");

        var ars = await _db.FinanceRecords.Where(x => x.RecordType == "receivable" && x.Status != "done" && (today - (x.RecordDate ?? x.CreatedAt).Date).Days >= 30).ToListAsync();
        foreach (var ar in ars) await AddIfMissing("AR", ar.Id, (today - (ar.RecordDate ?? ar.CreatedAt).Date).Days >= 60 ? "danger" : "warning", $"应收超期 {ar.No}", $"应收账款超过30天未结清，余额 {ar.Amount - ar.PaidAmount}");

        var aps = await _db.FinanceRecords.Where(x => x.RecordType == "payable" && x.Status != "done" && (today - (x.RecordDate ?? x.CreatedAt).Date).Days >= 30).ToListAsync();
        foreach (var ap in aps) await AddIfMissing("AP", ap.Id, (today - (ap.RecordDate ?? ap.CreatedAt).Date).Days >= 60 ? "danger" : "warning", $"应付超期 {ap.No}", $"应付账款超过30天未结清，余额 {ap.Amount - ap.PaidAmount}");

        await _db.SaveChangesAsync();
        return new { created };
    }

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id)
    {
        var entity = await _db.Notifications.FindAsync(id);
        if (entity == null) return NotFound();
        entity.Status = "read";
        entity.ReadAt = DateTime.Now;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll()
    {
        var rows = await _db.Notifications.Where(x => x.Status == "unread").ToListAsync();
        foreach (var row in rows) { row.Status = "read"; row.ReadAt = DateTime.Now; row.UpdatedAt = DateTime.Now; }
        await _db.SaveChangesAsync();
        return Ok(new { ok = true, count = rows.Count });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.Notifications.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
