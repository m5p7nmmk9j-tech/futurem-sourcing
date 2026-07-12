using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/warehouses")]
public sealed class WarehousesController : ControllerBase
{
    private readonly AppDbContext _db;

    public WarehousesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        var query = _db.Warehouses.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var rows = await query.OrderBy(x => x.Code).ToListAsync();
        var warehouseIds = rows.Select(x => x.Id).ToList();
        var locationCounts = await _db.WarehouseLocations
            .Where(x => warehouseIds.Contains(x.WarehouseId))
            .GroupBy(x => x.WarehouseId)
            .Select(x => new { WarehouseId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.WarehouseId, x => x.Count);
        return Ok(rows.Select(x => new { warehouse = x, locationCount = locationCounts.GetValueOrDefault(x.Id) }));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        var warehouse = await _db.Warehouses.FindAsync(id);
        if (warehouse is null) return NotFound();
        var locations = await _db.WarehouseLocations.Where(x => x.WarehouseId == id).OrderBy(x => x.Code).ToListAsync();
        return Ok(new { warehouse, locations });
    }

    [HttpPost]
    public async Task<ActionResult<Warehouse>> Create(Warehouse input)
    {
        input.Id = 0;
        input.Code = input.Code.Trim();
        input.Name = input.Name.Trim();
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "active" : input.Status.Trim();
        if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name))
            throw new BusinessRuleException("WAREHOUSE_REQUIRED_FIELDS", "仓库编码和名称不能为空");
        if (await _db.Warehouses.AnyAsync(x => x.Code == input.Code))
            throw new BusinessRuleException("WAREHOUSE_CODE_EXISTS", "仓库编码已存在");
        input.CreatedAt = DateTime.Now;
        _db.Warehouses.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<Warehouse>> Update(long id, Warehouse input)
    {
        var entity = await _db.Warehouses.FindAsync(id);
        if (entity is null) return NotFound();
        var code = input.Code.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(input.Name))
            throw new BusinessRuleException("WAREHOUSE_REQUIRED_FIELDS", "仓库编码和名称不能为空");
        if (await _db.Warehouses.AnyAsync(x => x.Id != id && x.Code == code))
            throw new BusinessRuleException("WAREHOUSE_CODE_EXISTS", "仓库编码已存在");
        entity.Code = code;
        entity.Name = input.Name.Trim();
        entity.Address = input.Address?.Trim();
        entity.ContactName = input.ContactName?.Trim();
        entity.ContactPhone = input.ContactPhone?.Trim();
        entity.Status = string.IsNullOrWhiteSpace(input.Status) ? entity.Status : input.Status.Trim();
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }
}
