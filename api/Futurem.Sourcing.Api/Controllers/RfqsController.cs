using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/rfqs")]
public class RfqsController : ControllerBase
{
    private readonly AppDbContext _db;

    public RfqsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Rfq>>> List([FromQuery] long? customerId)
    {
        var query = _db.Rfqs.AsQueryable();
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        return await query.OrderByDescending(x => x.Id).Take(200).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<Rfq>> Get(long id)
    {
        var entity = await _db.Rfqs.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<Rfq>> Create(Rfq input)
    {
        input.Id = 0;
        input.No = string.IsNullOrWhiteSpace(input.No) ? NumberService.NewNo("RFQ") : input.No;
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "draft" : input.Status;
        input.CreatedAt = DateTime.Now;
        _db.Rfqs.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPost("{id:long}/copy")]
    public async Task<ActionResult<Rfq>> Copy(long id)
    {
        var source = await _db.Rfqs.FindAsync(id);
        if (source == null) return NotFound();

        var copy = new Rfq
        {
            No = NumberService.NewNo("RFQ"),
            BuyingTripId = source.BuyingTripId,
            CustomerId = source.CustomerId,
            Status = "draft",
            RequestDate = DateTime.Today,
            Remark = $"复制自 {source.No}",
            CreatedAt = DateTime.Now
        };
        _db.Rfqs.Add(copy);
        await _db.SaveChangesAsync();
        await DocumentLineCopyService.CopyAsync(_db, "RFQ", source.Id, "RFQ", copy.Id);
        await _db.SaveChangesAsync();
        return copy;
    }

    [HttpPost("{id:long}/generate-co")]
    public async Task<ActionResult<CustomerOrder>> GenerateCo(long id)
    {
        var source = await _db.Rfqs.FindAsync(id);
        if (source == null) return NotFound();

        var co = new CustomerOrder
        {
            No = NumberService.NewNo("CO"),
            BuyingTripId = source.BuyingTripId,
            CustomerId = source.CustomerId,
            RfqId = source.Id,
            OrderDate = DateTime.Today,
            Currency = "RMB",
            Status = "draft",
            Remark = $"由 RFQ {source.No} 生成",
            CreatedAt = DateTime.Now
        };
        _db.CustomerOrders.Add(co);
        source.Status = "converted";
        source.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        await DocumentLineCopyService.CopyAsync(_db, "RFQ", source.Id, "CO", co.Id);
        await _db.SaveChangesAsync();
        return co;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<Rfq>> Update(long id, Rfq input)
    {
        var entity = await _db.Rfqs.FindAsync(id);
        if (entity == null) return NotFound();

        entity.CustomerId = input.CustomerId;
        entity.BuyingTripId = input.BuyingTripId;
        entity.Status = input.Status;
        entity.RequestDate = input.RequestDate;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.Rfqs.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
