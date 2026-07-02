using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/markets")]
public class MarketsController : ControllerBase
{
    private readonly AppDbContext _db;

    public MarketsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Market>>> List([FromQuery] string? keyword)
    {
        var query = _db.Markets.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || x.Code.Contains(keyword) || (x.City != null && x.City.Contains(keyword)));
        }

        return await query.OrderBy(x => x.Code).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<Market>> Get(long id)
    {
        var entity = await _db.Markets.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<Market>> Create(Market input)
    {
        input.Id = 0;
        input.Code = string.IsNullOrWhiteSpace(input.Code) ? NumberService.NewMarketCode() : input.Code;
        input.CreatedAt = DateTime.Now;
        _db.Markets.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<Market>> Update(long id, Market input)
    {
        var entity = await _db.Markets.FindAsync(id);
        if (entity == null) return NotFound();

        entity.Name = input.Name;
        entity.City = input.City;
        entity.Address = input.Address;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.Markets.FindAsync(id);
        if (entity == null) return NotFound();

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
