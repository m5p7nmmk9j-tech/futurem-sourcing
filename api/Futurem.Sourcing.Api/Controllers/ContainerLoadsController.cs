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

    public record GenerateFromSoRequest(long SummaryOrderId, string? ContainerType, DateTime? LoadDate);

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

    [HttpGet("recommend")]
    public async Task<ActionResult<object>> Recommend([FromQuery] long? summaryOrderId = null, [FromQuery] long? containerLoadId = null)
    {
        decimal cbm = 0, gw = 0, cartons = 0;
        if (containerLoadId.HasValue)
        {
            var cl = await _db.ContainerLoads.FindAsync(containerLoadId.Value);
            if (cl == null) return NotFound();
            var lines = await _db.DocumentLines.Where(x => x.DocumentType == "CL" && x.DocumentId == cl.Id).ToListAsync();
            cbm = lines.Sum(x => x.TotalCbm);
            gw = lines.Sum(x => x.TotalGwKg);
            cartons = lines.Sum(x => x.Cartons);
        }
        else if (summaryOrderId.HasValue)
        {
            var so = await _db.SummaryOrders.FindAsync(summaryOrderId.Value);
            if (so == null) return NotFound();
            var lines = await _db.DocumentLines.Where(x => x.DocumentType == "SO" && x.DocumentId == so.Id).ToListAsync();
            cbm = lines.Sum(x => x.TotalCbm);
            gw = lines.Sum(x => x.TotalGwKg);
            cartons = lines.Sum(x => x.Cartons);
        }
        else return BadRequest("summaryOrderId or containerLoadId required");

        var options = new[] { "20GP", "40GP", "40HQ", "45HQ" }.Select(t =>
        {
            var cap = GetCapacity(t);
            var cbmRate = cap.Cbm <= 0 ? 0 : Math.Round(cbm / cap.Cbm * 100, 2);
            var weightRate = cap.Kg <= 0 ? 0 : Math.Round(gw / cap.Kg * 100, 2);
            var ok = cbm <= cap.Cbm && gw <= cap.Kg;
            return new { containerType = t, capacityCbm = cap.Cbm, capacityKg = cap.Kg, cbmRate, weightRate, remainingCbm = cap.Cbm - cbm, remainingKg = cap.Kg - gw, ok };
        }).ToList();
        var recommended = options.FirstOrDefault(x => x.ok) ?? options.Last();
        return new
        {
            cartons,
            cbm,
            gw,
            recommended = recommended.containerType,
            needSplit = !options.Any(x => x.ok),
            message = options.Any(x => x.ok) ? $"建议使用 {recommended.containerType}" : "单柜无法装完，建议拆分多柜或重新分配装柜",
            options
        };
    }

    [HttpGet("{id:long}/utilization")]
    public async Task<ActionResult<object>> Utilization(long id)
    {
        var cl = await _db.ContainerLoads.FindAsync(id);
        if (cl == null) return NotFound();
        var lines = await _db.DocumentLines.Where(x => x.DocumentType == "CL" && x.DocumentId == cl.Id).ToListAsync();
        var cartons = lines.Sum(x => x.Cartons);
        var cbm = lines.Sum(x => x.TotalCbm);
        var gw = lines.Sum(x => x.TotalGwKg);
        var cap = GetCapacity(cl.ContainerType);
        var cbmRate = cap.Cbm <= 0 ? 0 : Math.Round(cbm / cap.Cbm * 100, 2);
        var weightRate = cap.Kg <= 0 ? 0 : Math.Round(gw / cap.Kg * 100, 2);
        var overCbm = cbm > cap.Cbm;
        var overWeight = gw > cap.Kg;
        var level = overCbm || overWeight ? "danger" : cbmRate >= 95 || weightRate >= 95 ? "warning" : "ok";
        return new
        {
            containerType = cl.ContainerType,
            capacityCbm = cap.Cbm,
            capacityKg = cap.Kg,
            cartons,
            cbm,
            gw,
            remainingCbm = cap.Cbm - cbm,
            remainingKg = cap.Kg - gw,
            cbmRate,
            weightRate,
            overCbm,
            overWeight,
            level,
            message = level == "danger" ? "已超柜，请拆分或换更大柜型" : level == "warning" ? "接近满柜，请谨慎继续添加货物" : "容量正常"
        };
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

    [HttpPost("generate-from-so")]
    public async Task<ActionResult<ContainerLoad>> GenerateFromSo(GenerateFromSoRequest request)
    {
        if (request.SummaryOrderId <= 0) return BadRequest("SummaryOrderId required");
        var so = await _db.SummaryOrders.FindAsync(request.SummaryOrderId);
        if (so == null) return NotFound();

        var lines = await _db.DocumentLines.Where(x => x.DocumentType == "SO" && x.DocumentId == so.Id).ToListAsync();
        var cl = new ContainerLoad
        {
            No = NumberService.NewNo("CL"),
            SummaryOrderId = so.Id,
            ContainerType = string.IsNullOrWhiteSpace(request.ContainerType) ? "40HQ" : request.ContainerType!,
            LoadDate = request.LoadDate ?? DateTime.Today,
            Status = "draft",
            TotalCartons = lines.Sum(x => x.Cartons),
            TotalCbm = lines.Sum(x => x.TotalCbm),
            TotalGwKg = lines.Sum(x => x.TotalGwKg),
            Remark = $"由 SO {so.No} 生成",
            CreatedAt = DateTime.Now
        };
        _db.ContainerLoads.Add(cl);
        so.Status = "container_created";
        so.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        await DocumentLineCopyService.CopyAsync(_db, "SO", so.Id, "CL", cl.Id);
        await _db.SaveChangesAsync();
        return cl;
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

    private static (decimal Cbm, decimal Kg) GetCapacity(string? containerType)
    {
        return (containerType ?? "40HQ").ToUpperInvariant() switch
        {
            "20GP" => (28m, 21600m),
            "40GP" => (58m, 26500m),
            "40HQ" => (68m, 26500m),
            "45HQ" => (78m, 28000m),
            _ => (68m, 26500m)
        };
    }
}
