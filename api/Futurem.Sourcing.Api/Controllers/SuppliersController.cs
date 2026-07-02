using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
public class SuppliersController : ControllerBase
{
    private readonly AppDbContext _db;

    public SuppliersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Supplier>>> List([FromQuery] string? keyword)
    {
        var query = _db.Suppliers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || x.Code.Contains(keyword) || (x.ShopNo != null && x.ShopNo.Contains(keyword)) || (x.Whatsapp != null && x.Whatsapp.Contains(keyword)));
        }

        return await query.OrderByDescending(x => x.Id).Take(200).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<Supplier>> Get(long id)
    {
        var entity = await _db.Suppliers.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<Supplier>> Create(Supplier input)
    {
        input.Id = 0;
        input.Code = string.IsNullOrWhiteSpace(input.Code) ? NumberService.NewSupplierCode() : input.Code;
        input.CreatedAt = DateTime.Now;
        _db.Suppliers.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<Supplier>> Update(long id, Supplier input)
    {
        var entity = await _db.Suppliers.FindAsync(id);
        if (entity == null) return NotFound();

        entity.Name = input.Name;
        entity.MarketId = input.MarketId;
        entity.ShopNo = input.ShopNo;
        entity.FloorNo = input.FloorNo;
        entity.BoothNo = input.BoothNo;
        entity.MainProducts = input.MainProducts;
        entity.ContactName = input.ContactName;
        entity.Phone = input.Phone;
        entity.Wechat = input.Wechat;
        entity.Whatsapp = input.Whatsapp;
        entity.Email = input.Email;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.Suppliers.FindAsync(id);
        if (entity == null) return NotFound();

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
