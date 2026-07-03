using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/receiving-orders")]
public class ReceivingOrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    public ReceivingOrdersController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReceivingOrder>>> List([FromQuery] long? purchaseOrderId)
    {
        var query = _db.ReceivingOrders.AsQueryable();
        if (purchaseOrderId.HasValue) query = query.Where(x => x.PurchaseOrderId == purchaseOrderId.Value);
        return await query.OrderByDescending(x => x.Id).Take(200).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ReceivingOrder>> Get(long id)
    {
        var entity = await _db.ReceivingOrders.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<ReceivingOrder>> Create(ReceivingOrder input)
    {
        input.Id = 0;
        input.No = string.IsNullOrWhiteSpace(input.No) ? NumberService.NewNo("RCV") : input.No;
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "draft" : input.Status;
        input.CreatedAt = DateTime.Now;
        _db.ReceivingOrders.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPost("{id:long}/copy")]
    public async Task<ActionResult<ReceivingOrder>> Copy(long id)
    {
        var source = await _db.ReceivingOrders.FindAsync(id);
        if (source == null) return NotFound();
        var copy = new ReceivingOrder
        {
            No = NumberService.NewNo("RCV"),
            PurchaseOrderId = source.PurchaseOrderId,
            ReceiveDate = DateTime.Today,
            WarehouseLocation = source.WarehouseLocation,
            Status = "draft",
            Remark = $"复制自 {source.No}",
            CreatedAt = DateTime.Now
        };
        _db.ReceivingOrders.Add(copy);
        await _db.SaveChangesAsync();
        await DocumentLineCopyService.CopyAsync(_db, "RCV", source.Id, "RCV", copy.Id);
        await _db.SaveChangesAsync();
        return copy;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ReceivingOrder>> Update(long id, ReceivingOrder input)
    {
        var entity = await _db.ReceivingOrders.FindAsync(id);
        if (entity == null) return NotFound();
        entity.PurchaseOrderId = input.PurchaseOrderId;
        entity.ReceiveDate = input.ReceiveDate;
        entity.WarehouseLocation = input.WarehouseLocation;
        entity.Status = input.Status;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.ReceivingOrders.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
