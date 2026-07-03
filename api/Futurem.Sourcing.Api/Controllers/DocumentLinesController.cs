using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/document-lines")]
public class DocumentLinesController : ControllerBase
{
    private readonly AppDbContext _db;
    public DocumentLinesController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentLine>>> List([FromQuery] string documentType, [FromQuery] long documentId)
    {
        return await _db.DocumentLines
            .Where(x => x.DocumentType == documentType && x.DocumentId == documentId)
            .OrderBy(x => x.SortNo)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }

    [HttpGet("summary")]
    public async Task<ActionResult<object>> Summary([FromQuery] string documentType, [FromQuery] long documentId)
    {
        var lines = await _db.DocumentLines
            .Where(x => x.DocumentType == documentType && x.DocumentId == documentId)
            .ToListAsync();

        return new
        {
            quantity = lines.Sum(x => x.Quantity),
            amount = lines.Sum(x => x.Amount),
            cartons = lines.Sum(x => x.Cartons),
            cbm = lines.Sum(x => x.TotalCbm),
            gwKg = lines.Sum(x => x.TotalGwKg),
            nwKg = lines.Sum(x => x.TotalNwKg)
        };
    }

    [HttpPost]
    public async Task<ActionResult<DocumentLine>> Create(DocumentLine input)
    {
        input.Id = 0;
        Calculate(input);
        input.CreatedAt = DateTime.Now;
        _db.DocumentLines.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<DocumentLine>> Update(long id, DocumentLine input)
    {
        var entity = await _db.DocumentLines.FindAsync(id);
        if (entity == null) return NotFound();

        entity.ProductId = input.ProductId;
        entity.Sku = input.Sku;
        entity.ProductName = input.ProductName;
        entity.Unit = input.Unit;
        entity.Quantity = input.Quantity;
        entity.UnitPrice = input.UnitPrice;
        entity.CartonQty = input.CartonQty;
        entity.Cartons = input.Cartons;
        entity.CartonLengthCm = input.CartonLengthCm;
        entity.CartonWidthCm = input.CartonWidthCm;
        entity.CartonHeightCm = input.CartonHeightCm;
        entity.CartonGwKg = input.CartonGwKg;
        entity.CartonNwKg = input.CartonNwKg;
        entity.SupplierItemNo = input.SupplierItemNo;
        entity.CustomerItemNo = input.CustomerItemNo;
        entity.Remark = input.Remark;
        entity.SortNo = input.SortNo;
        Calculate(entity);
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.DocumentLines.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    private static void Calculate(DocumentLine line)
    {
        line.Amount = line.Quantity * line.UnitPrice;
        if (line.CartonQty > 0 && line.Cartons <= 0) line.Cartons = Math.Ceiling(line.Quantity / line.CartonQty);
        line.CartonCbm = line.CartonLengthCm * line.CartonWidthCm * line.CartonHeightCm / 1000000m;
        line.TotalCbm = line.CartonCbm * line.Cartons;
        line.TotalGwKg = line.CartonGwKg * line.Cartons;
        line.TotalNwKg = line.CartonNwKg * line.Cartons;
    }
}
