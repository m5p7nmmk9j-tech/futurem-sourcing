using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/warehouse-locations")]
public sealed class WarehouseLocationsController : ControllerBase
{
    private readonly AppDbContext _db;

    public WarehouseLocationsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] long? warehouseId, [FromQuery] string? status)
    {
        var query = _db.WarehouseLocations.AsQueryable();
        if (warehouseId.HasValue) query = query.Where(x => x.WarehouseId == warehouseId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return Ok(await query.OrderBy(x => x.WarehouseId).ThenBy(x => x.Code).ToListAsync());
    }

    [HttpPost]
    public async Task<ActionResult<WarehouseLocation>> Create(WarehouseLocation input)
    {
        if (!await _db.Warehouses.AnyAsync(x => x.Id == input.WarehouseId))
            throw new BusinessRuleException("WAREHOUSE_NOT_FOUND", "仓库不存在");
        input.Id = 0;
        input.Code = input.Code.Trim();
        input.Name = input.Name.Trim();
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "active" : input.Status.Trim();
        if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name))
            throw new BusinessRuleException("WAREHOUSE_LOCATION_REQUIRED_FIELDS", "库位编码和名称不能为空");
        if (await _db.WarehouseLocations.AnyAsync(x => x.WarehouseId == input.WarehouseId && x.Code == input.Code))
            throw new BusinessRuleException("WAREHOUSE_LOCATION_CODE_EXISTS", "当前仓库已存在相同库位编码");
        input.CreatedAt = DateTime.Now;
        _db.WarehouseLocations.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<WarehouseLocation>> Update(long id, WarehouseLocation input)
    {
        var entity = await _db.WarehouseLocations.FindAsync(id);
        if (entity is null) return NotFound();
        if (!await _db.Warehouses.AnyAsync(x => x.Id == input.WarehouseId))
            throw new BusinessRuleException("WAREHOUSE_NOT_FOUND", "仓库不存在");
        var code = input.Code.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(input.Name))
            throw new BusinessRuleException("WAREHOUSE_LOCATION_REQUIRED_FIELDS", "库位编码和名称不能为空");
        if (await _db.WarehouseLocations.AnyAsync(x => x.Id != id && x.WarehouseId == input.WarehouseId && x.Code == code))
            throw new BusinessRuleException("WAREHOUSE_LOCATION_CODE_EXISTS", "当前仓库已存在相同库位编码");
        entity.WarehouseId = input.WarehouseId;
        entity.Code = code;
        entity.Name = input.Name.Trim();
        entity.Zone = input.Zone?.Trim();
        entity.Aisle = input.Aisle?.Trim();
        entity.Rack = input.Rack?.Trim();
        entity.Bin = input.Bin?.Trim();
        entity.Status = string.IsNullOrWhiteSpace(input.Status) ? entity.Status : input.Status.Trim();
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }
}
