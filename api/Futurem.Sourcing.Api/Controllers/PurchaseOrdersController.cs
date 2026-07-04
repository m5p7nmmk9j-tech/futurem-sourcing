using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/purchase-orders")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    public PurchaseOrdersController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PurchaseOrder>>> List([FromQuery] long? supplierId, [FromQuery] long? customerId)
    {
        var query = _db.PurchaseOrders.AsQueryable();
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value);
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        return await query.OrderByDescending(x => x.Id).Take(200).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<PurchaseOrder>> Get(long id)
    {
        var entity = await _db.PurchaseOrders.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<PurchaseOrder>> Create(PurchaseOrder input)
    {
        input.Id = 0;
        input.No = string.IsNullOrWhiteSpace(input.No) ? NumberService.NewNo("PO") : input.No;
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "draft" : input.Status;
        input.PayStatus = string.IsNullOrWhiteSpace(input.PayStatus) ? "unpaid" : input.PayStatus;
        input.CreatedAt = DateTime.Now;
        _db.PurchaseOrders.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPost("{id:long}/generate-payable")]
    public async Task<ActionResult<FinanceRecord>> GeneratePayable(long id)
    {
        var po = await _db.PurchaseOrders.FindAsync(id);
        if (po == null) return NotFound();
        var amount = await FinanceAutoService.SumDocumentAmountAsync(_db, "PO", po.Id);
        var finance = await FinanceAutoService.EnsurePayableAsync(_db, "PO", po.Id, po.SupplierId, po.CustomerId, po.Currency, amount, $"由 PO {po.No} 自动生成应付");
        po.PayStatus = finance.Status == "done" ? "paid" : finance.Status == "partial" ? "partial" : "unpaid";
        po.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return finance;
    }

    [HttpPost("{id:long}/copy")]
    public async Task<ActionResult<PurchaseOrder>> Copy(long id)
    {
        var source = await _db.PurchaseOrders.FindAsync(id);
        if (source == null) return NotFound();
        var copy = new PurchaseOrder
        {
            No = NumberService.NewNo("PO"),
            BuyingTripId = source.BuyingTripId,
            CustomerOrderId = source.CustomerOrderId,
            SupplierId = source.SupplierId,
            CustomerId = source.CustomerId,
            OrderDate = DateTime.Today,
            ExpectedDeliveryDate = source.ExpectedDeliveryDate,
            Currency = source.Currency,
            Status = "draft",
            PayStatus = "unpaid",
            Remark = $"复制自 {source.No}",
            CreatedAt = DateTime.Now
        };
        _db.PurchaseOrders.Add(copy);
        await _db.SaveChangesAsync();
        await DocumentLineCopyService.CopyAsync(_db, "PO", source.Id, "PO", copy.Id);
        await _db.SaveChangesAsync();
        return copy;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<PurchaseOrder>> Update(long id, PurchaseOrder input)
    {
        var entity = await _db.PurchaseOrders.FindAsync(id);
        if (entity == null) return NotFound();
        entity.CustomerOrderId = input.CustomerOrderId;
        entity.SupplierId = input.SupplierId;
        entity.CustomerId = input.CustomerId;
        entity.OrderDate = input.OrderDate;
        entity.ExpectedDeliveryDate = input.ExpectedDeliveryDate;
        entity.Currency = input.Currency;
        entity.Status = input.Status;
        entity.PayStatus = input.PayStatus;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.PurchaseOrders.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
