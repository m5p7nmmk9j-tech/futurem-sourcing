using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/customer-orders")]
public class CustomerOrdersController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomerOrdersController(AppDbContext db)
    {
        _db = db;
    }

    public record GeneratePoRequest(long SupplierId, DateTime? ExpectedDeliveryDate, string? Currency);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CustomerOrder>>> List([FromQuery] long? customerId)
    {
        var query = _db.CustomerOrders.AsQueryable();
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        return await query.OrderByDescending(x => x.Id).Take(200).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<CustomerOrder>> Get(long id)
    {
        var entity = await _db.CustomerOrders.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<CustomerOrder>> Create(CustomerOrder input)
    {
        input.Id = 0;
        input.No = string.IsNullOrWhiteSpace(input.No) ? NumberService.NewNo("CO") : input.No;
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "draft" : input.Status;
        input.CreatedAt = DateTime.Now;
        _db.CustomerOrders.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPost("{id:long}/copy")]
    public async Task<ActionResult<CustomerOrder>> Copy(long id)
    {
        var source = await _db.CustomerOrders.FindAsync(id);
        if (source == null) return NotFound();

        var copy = new CustomerOrder
        {
            No = NumberService.NewNo("CO"),
            BuyingTripId = source.BuyingTripId,
            CustomerId = source.CustomerId,
            RfqId = source.RfqId,
            OrderDate = DateTime.Today,
            Currency = source.Currency,
            Status = "draft",
            Remark = $"复制自 {source.No}",
            CreatedAt = DateTime.Now
        };
        _db.CustomerOrders.Add(copy);
        await _db.SaveChangesAsync();
        await DocumentLineCopyService.CopyAsync(_db, "CO", source.Id, "CO", copy.Id);
        await _db.SaveChangesAsync();
        return copy;
    }

    [HttpPost("{id:long}/generate-po")]
    public async Task<ActionResult<PurchaseOrder>> GeneratePo(long id, GeneratePoRequest request)
    {
        var source = await _db.CustomerOrders.FindAsync(id);
        if (source == null) return NotFound();
        if (request.SupplierId <= 0) return BadRequest("SupplierId required");

        var po = new PurchaseOrder
        {
            No = NumberService.NewNo("PO"),
            BuyingTripId = source.BuyingTripId,
            CustomerOrderId = source.Id,
            SupplierId = request.SupplierId,
            CustomerId = source.CustomerId,
            OrderDate = DateTime.Today,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "CNY" : request.Currency!,
            Status = "draft",
            PayStatus = "unpaid",
            Remark = $"由 CO {source.No} 生成",
            CreatedAt = DateTime.Now
        };
        _db.PurchaseOrders.Add(po);
        source.Status = "converted";
        source.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        await DocumentLineCopyService.CopyAsync(_db, "CO", source.Id, "PO", po.Id);
        await _db.SaveChangesAsync();
        return po;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<CustomerOrder>> Update(long id, CustomerOrder input)
    {
        var entity = await _db.CustomerOrders.FindAsync(id);
        if (entity == null) return NotFound();

        entity.CustomerId = input.CustomerId;
        entity.BuyingTripId = input.BuyingTripId;
        entity.RfqId = input.RfqId;
        entity.OrderDate = input.OrderDate;
        entity.Currency = input.Currency;
        entity.Status = input.Status;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.CustomerOrders.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
