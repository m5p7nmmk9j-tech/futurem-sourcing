using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> List([FromQuery] string? keyword)
    {
        var query = _db.Products.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.Sku.Contains(keyword) ||
                x.Barcode.Contains(keyword) ||
                x.NameCn.Contains(keyword) ||
                (x.NameEn != null && x.NameEn.Contains(keyword)) ||
                (x.NameEs != null && x.NameEs.Contains(keyword)) ||
                (x.CustomerItemNo != null && x.CustomerItemNo.Contains(keyword)));
        }

        return await query.OrderByDescending(x => x.Id).Take(200).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<Product>> Get(long id)
    {
        var entity = await _db.Products.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<Product>> Create(Product input)
    {
        input.Id = 0;
        input.Sku = string.IsNullOrWhiteSpace(input.Sku) ? NumberService.NewProductSku() : input.Sku;
        input.Barcode = string.IsNullOrWhiteSpace(input.Barcode) ? NumberService.NewProductBarcode() : input.Barcode;
        input.CreatedAt = DateTime.Now;
        _db.Products.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<Product>> Update(long id, Product input)
    {
        var entity = await _db.Products.FindAsync(id);
        if (entity == null) return NotFound();

        entity.NameCn = input.NameCn;
        entity.NameEn = input.NameEn;
        entity.NameEs = input.NameEs;
        entity.CategoryId = input.CategoryId;
        entity.Brand = input.Brand;
        entity.Unit = input.Unit;
        entity.CustomerItemNo = input.CustomerItemNo;
        entity.ImageUrl = input.ImageUrl;
        entity.PurchasePrice = input.PurchasePrice;
        entity.CartonQty = input.CartonQty;
        entity.CartonLengthCm = input.CartonLengthCm;
        entity.CartonWidthCm = input.CartonWidthCm;
        entity.CartonHeightCm = input.CartonHeightCm;
        entity.CartonGwKg = input.CartonGwKg;
        entity.CartonNwKg = input.CartonNwKg;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.Products.FindAsync(id);
        if (entity == null) return NotFound();

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
