using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/shipments")]
public class ShipmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ShipmentsController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Shipment>>> List([FromQuery] long? containerLoadId, [FromQuery] long? summaryOrderId)
    {
        var query = _db.Shipments.AsQueryable();
        if (containerLoadId.HasValue) query = query.Where(x => x.ContainerLoadId == containerLoadId.Value);
        if (summaryOrderId.HasValue) query = query.Where(x => x.SummaryOrderId == summaryOrderId.Value);
        return await query.OrderByDescending(x => x.Id).Take(200).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<Shipment>> Get(long id)
    {
        var entity = await _db.Shipments.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<Shipment>> Create(Shipment input)
    {
        input.Id = 0;
        input.No = string.IsNullOrWhiteSpace(input.No) ? NumberService.NewNo("SHP") : input.No;
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "draft" : input.Status;
        input.ShipmentMode = string.IsNullOrWhiteSpace(input.ShipmentMode) ? "SEA" : input.ShipmentMode;
        input.CreatedAt = DateTime.Now;
        _db.Shipments.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<Shipment>> Update(long id, Shipment input)
    {
        var entity = await _db.Shipments.FindAsync(id);
        if (entity == null) return NotFound();
        entity.ContainerLoadId = input.ContainerLoadId;
        entity.SummaryOrderId = input.SummaryOrderId;
        entity.ShipmentMode = input.ShipmentMode;
        entity.Carrier = input.Carrier;
        entity.VesselVoyage = input.VesselVoyage;
        entity.BillOfLadingNo = input.BillOfLadingNo;
        entity.DeparturePort = input.DeparturePort;
        entity.DestinationPort = input.DestinationPort;
        entity.Etd = input.Etd;
        entity.Eta = input.Eta;
        entity.Status = input.Status;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.Shipments.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
