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
    public async Task<ActionResult<IEnumerable<ReceivingOrder>>> List(
        [FromQuery] long? purchaseOrderId,
        [FromQuery] long? deliveryNoticeId,
        [FromQuery] long? warehouseId)
    {
        var query = _db.ReceivingOrders.AsQueryable();
        if (purchaseOrderId.HasValue) query = query.Where(x => x.PurchaseOrderId == purchaseOrderId.Value);
        if (deliveryNoticeId.HasValue) query = query.Where(x => x.DeliveryNoticeId == deliveryNoticeId.Value);
        if (warehouseId.HasValue) query = query.Where(x => x.WarehouseId == warehouseId.Value);
        return await query.OrderByDescending(x => x.Id).Take(300).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        var entity = await _db.ReceivingOrders.FindAsync(id);
        if (entity == null) return NotFound();
        var lines = await _db.DocumentLines
            .Where(x => x.DocumentType == "RCV" && x.DocumentId == id && !x.IsDeleted)
            .OrderBy(x => x.SortNo)
            .ToListAsync();
        return Ok(new { receivingOrder = entity, lines });
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
        if (source.DeliveryNoticeId.HasValue)
            throw new BusinessRuleException("DELIVERY_RECEIVING_COPY_NOT_ALLOWED", "送货通知生成的收货单不能复制，请从原通知继续创建分批收货");

        var copy = new ReceivingOrder
        {
            No = NumberService.NewNo("RCV"),
            PurchaseOrderId = source.PurchaseOrderId,
            WarehouseId = source.WarehouseId,
            SupplierId = source.SupplierId,
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
        if (entity.DeliveryNoticeId.HasValue && entity.Status != "draft")
            throw new BusinessRuleException("RECEIVING_LOCKED", "送货通知生成的收货记录不能直接覆盖修改");

        entity.PurchaseOrderId = input.PurchaseOrderId;
        entity.DeliveryNoticeId = input.DeliveryNoticeId;
        entity.WarehouseId = input.WarehouseId;
        entity.SupplierId = input.SupplierId;
        entity.ReceiveDate = input.ReceiveDate;
        entity.WarehouseLocation = input.WarehouseLocation;
        entity.Status = input.Status;
        entity.TemporaryQuantity = input.TemporaryQuantity;
        entity.TemporaryCartons = input.TemporaryCartons;
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
        if (entity.DeliveryNoticeId.HasValue)
            throw new BusinessRuleException("DELIVERY_RECEIVING_DELETE_NOT_ALLOWED", "送货通知生成的收货记录不能直接删除");

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
