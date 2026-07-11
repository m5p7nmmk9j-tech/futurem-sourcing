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
        input.Sku = string.IsNullOrWhiteSpace(input.Sku) ? await NewUniqueSkuAsync() : input.Sku.Trim();
        input.Barcode = string.IsNullOrWhiteSpace(input.Barcode) ? await NewUniqueBarcodeAsync() : input.Barcode.Trim();
        if (input.Sku.Length > 80) return BadRequest("SKU length must be <= 80");
        if (input.Barcode.Length > 80) return BadRequest("Barcode length must be <= 80");
        if (await _db.Products.IgnoreQueryFilters().AnyAsync(x => x.Sku == input.Sku)) return BadRequest("SKU already exists");
        if (await _db.Products.IgnoreQueryFilters().AnyAsync(x => x.Barcode == input.Barcode)) return BadRequest("Barcode already exists");
        RoundProductValues(input);
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

        var barcode = input.Barcode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(barcode)) return BadRequest("Barcode required");
        if (barcode.Length > 80) return BadRequest("Barcode length must be <= 80");
        if (await _db.Products.IgnoreQueryFilters().AnyAsync(x => x.Id != id && x.Barcode == barcode))
            return BadRequest("Barcode already exists");

        entity.Barcode = barcode;
        entity.NameCn = input.NameCn;
        entity.NameEn = input.NameEn;
        entity.NameEs = input.NameEs;
        entity.CategoryId = input.CategoryId;
        entity.Brand = input.Brand;
        entity.Unit = input.Unit;
        entity.CustomerItemNo = input.CustomerItemNo;
        entity.ImageUrl = input.ImageUrl;
        entity.PurchasePrice = FinanceBalanceService.Round2(input.PurchasePrice);
        entity.CartonQty = FinanceBalanceService.Round2(input.CartonQty);
        entity.CartonLengthCm = FinanceBalanceService.Round2(input.CartonLengthCm);
        entity.CartonWidthCm = FinanceBalanceService.Round2(input.CartonWidthCm);
        entity.CartonHeightCm = FinanceBalanceService.Round2(input.CartonHeightCm);
        entity.CartonGwKg = FinanceBalanceService.Round2(input.CartonGwKg);
        entity.CartonNwKg = FinanceBalanceService.Round2(input.CartonNwKg);
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

    private static void RoundProductValues(Product product)
    {
        product.PurchasePrice = FinanceBalanceService.Round2(product.PurchasePrice);
        product.CartonQty = FinanceBalanceService.Round2(product.CartonQty);
        product.CartonLengthCm = FinanceBalanceService.Round2(product.CartonLengthCm);
        product.CartonWidthCm = FinanceBalanceService.Round2(product.CartonWidthCm);
        product.CartonHeightCm = FinanceBalanceService.Round2(product.CartonHeightCm);
        product.CartonGwKg = FinanceBalanceService.Round2(product.CartonGwKg);
        product.CartonNwKg = FinanceBalanceService.Round2(product.CartonNwKg);
    }

    private async Task<string> NewUniqueSkuAsync()
    {
        for (var i = 0; i < 20; i++)
        {
            var sku = NumberService.NewProductSku();
            if (!await _db.Products.IgnoreQueryFilters().AnyAsync(x => x.Sku == sku)) return sku;
        }
        throw new InvalidOperationException("Unable to generate unique product SKU");
    }

    private async Task<string> NewUniqueBarcodeAsync()
    {
        for (var i = 0; i < 20; i++)
        {
            var barcode = NumberService.NewProductBarcode();
            if (!await _db.Products.IgnoreQueryFilters().AnyAsync(x => x.Barcode == barcode)) return barcode;
        }
        throw new InvalidOperationException("Unable to generate unique product barcode");
    }
}
