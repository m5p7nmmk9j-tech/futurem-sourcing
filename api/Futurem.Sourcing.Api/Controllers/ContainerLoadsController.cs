using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/container-loads")]
public class ContainerLoadsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ContainerLoadsController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ContainerLoad>>> List([FromQuery] long? summaryOrderId)
    {
        var query = _db.ContainerLoads.AsQueryable();
        if (summaryOrderId.HasValue) query = query.Where(x => x.SummaryOrderId == summaryOrderId.Value);
        return await query.OrderByDescending(x => x.Id).Take(200).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ContainerLoad>> Get(long id)
    {
        var entity = await _db.ContainerLoads.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<ContainerLoad>> Create(ContainerLoad input)
    {
        input.Id = 0;
        input.No = string.IsNullOrWhiteSpace(input.No) ? NumberService.NewNo("CL") : input.No;
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "draft" : input.Status;
        input.CreatedAt = DateTime.Now;
        _db.ContainerLoads.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPost("{id:long}/copy")]
    public async Task<ActionResult<ContainerLoad>> Copy(long id)
    {
        var source = await _db.ContainerLoads.FindAsync(id);
        if (source == null) return NotFound();
        var copy = new ContainerLoad
        {
            No = NumberService.NewNo("CL"),
            SummaryOrderId = source.SummaryOrderId,
            ContainerType = source.ContainerType,
            LoadDate = DateTime.Today,
            Status = "draft",
            TotalCbm = source.TotalCbm,
            TotalGwKg = source.TotalGwKg,
            TotalCartons = source.TotalCartons,
            Remark = $"复制自 {source.No}",
            CreatedAt = DateTime.Now
        };
        _db.ContainerLoads.Add(copy);
        await _db.SaveChangesAsync();
        await DocumentLineCopyService.CopyAsync(_db, "CL", source.Id, "CL", copy.Id);
        await _db.SaveChangesAsync();
        return copy;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ContainerLoad>> Update(long id, ContainerLoad input)
    {
        var entity = await _db.ContainerLoads.FindAsync(id);
        if (entity == null) return NotFound();
        entity.SummaryOrderId = input.SummaryOrderId;
        entity.ContainerType = input.ContainerType;
        entity.ContainerNo = input.ContainerNo;
        entity.SealNo = input.SealNo;
        entity.LoadDate = input.LoadDate;
        entity.Status = input.Status;
        entity.TotalCbm = input.TotalCbm;
        entity.TotalGwKg = input.TotalGwKg;
        entity.TotalCartons = input.TotalCartons;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.ContainerLoads.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
