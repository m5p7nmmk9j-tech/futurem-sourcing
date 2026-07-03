using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/qc-orders")]
public class QcOrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    public QcOrdersController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<QcOrder>>> List([FromQuery] long? purchaseOrderId, [FromQuery] long? receivingOrderId)
    {
        var query = _db.QcOrders.AsQueryable();
        if (purchaseOrderId.HasValue) query = query.Where(x => x.PurchaseOrderId == purchaseOrderId.Value);
        if (receivingOrderId.HasValue) query = query.Where(x => x.ReceivingOrderId == receivingOrderId.Value);
        return await query.OrderByDescending(x => x.Id).Take(200).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<QcOrder>> Get(long id)
    {
        var entity = await _db.QcOrders.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<QcOrder>> Create(QcOrder input)
    {
        input.Id = 0;
        input.No = string.IsNullOrWhiteSpace(input.No) ? NumberService.NewNo("QC") : input.No;
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "draft" : input.Status;
        input.Result = string.IsNullOrWhiteSpace(input.Result) ? "pending" : input.Result;
        input.CreatedAt = DateTime.Now;
        _db.QcOrders.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPost("{id:long}/copy")]
    public async Task<ActionResult<QcOrder>> Copy(long id)
    {
        var source = await _db.QcOrders.FindAsync(id);
        if (source == null) return NotFound();
        var copy = new QcOrder
        {
            No = NumberService.NewNo("QC"),
            PurchaseOrderId = source.PurchaseOrderId,
            ReceivingOrderId = source.ReceivingOrderId,
            QcDate = DateTime.Today,
            Status = "draft",
            Result = "pending",
            Remark = $"复制自 {source.No}",
            CreatedAt = DateTime.Now
        };
        _db.QcOrders.Add(copy);
        await _db.SaveChangesAsync();
        await DocumentLineCopyService.CopyAsync(_db, "QC", source.Id, "QC", copy.Id);
        await _db.SaveChangesAsync();
        return copy;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<QcOrder>> Update(long id, QcOrder input)
    {
        var entity = await _db.QcOrders.FindAsync(id);
        if (entity == null) return NotFound();
        entity.PurchaseOrderId = input.PurchaseOrderId;
        entity.ReceivingOrderId = input.ReceivingOrderId;
        entity.QcDate = input.QcDate;
        entity.Status = input.Status;
        entity.Result = input.Result;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.QcOrders.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
