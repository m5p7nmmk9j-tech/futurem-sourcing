using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/finance-records")]
public class FinanceRecordsController : ControllerBase
{
    private readonly AppDbContext _db;
    public FinanceRecordsController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FinanceRecord>>> List([FromQuery] string? recordType, [FromQuery] string? targetType, [FromQuery] long? customerId, [FromQuery] long? supplierId)
    {
        var query = _db.FinanceRecords.AsQueryable();
        if (!string.IsNullOrWhiteSpace(recordType)) query = query.Where(x => x.RecordType == recordType);
        if (!string.IsNullOrWhiteSpace(targetType)) query = query.Where(x => x.TargetType == targetType);
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value);
        return await query.OrderByDescending(x => x.Id).Take(300).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<FinanceRecord>> Get(long id)
    {
        var entity = await _db.FinanceRecords.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<FinanceRecord>> Create(FinanceRecord input)
    {
        input.Id = 0;
        input.No = string.IsNullOrWhiteSpace(input.No) ? NumberService.NewNo("FIN") : input.No;
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "pending" : input.Status;
        input.CreatedAt = DateTime.Now;
        _db.FinanceRecords.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<FinanceRecord>> Update(long id, FinanceRecord input)
    {
        var entity = await _db.FinanceRecords.FindAsync(id);
        if (entity == null) return NotFound();
        entity.RecordType = input.RecordType;
        entity.TargetType = input.TargetType;
        entity.TargetId = input.TargetId;
        entity.CustomerId = input.CustomerId;
        entity.SupplierId = input.SupplierId;
        entity.Currency = input.Currency;
        entity.Amount = input.Amount;
        entity.PaidAmount = input.PaidAmount;
        entity.RecordDate = input.RecordDate;
        entity.Status = input.Status;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.FinanceRecords.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
