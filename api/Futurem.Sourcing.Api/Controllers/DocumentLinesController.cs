using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/document-lines")]
public class DocumentLinesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ShipmentMeasurementService _measurementService;

    public DocumentLinesController(AppDbContext db, ShipmentMeasurementService measurementService)
    {
        _db = db;
        _measurementService = measurementService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentLine>>> List([FromQuery] string documentType, [FromQuery] long documentId)
    {
        return await _db.DocumentLines
            .Where(x => !x.IsDeleted && x.DocumentType == documentType && x.DocumentId == documentId)
            .OrderBy(x => x.SortNo)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }

    [HttpGet("summary")]
    public async Task<ActionResult<object>> Summary([FromQuery] string documentType, [FromQuery] long documentId)
    {
        var lines = await _db.DocumentLines
            .Where(x => !x.IsDeleted && x.DocumentType == documentType && x.DocumentId == documentId)
            .ToListAsync();
        foreach (var line in lines) Calculate(line);

        return new
        {
            quantity = FinanceBalanceService.Round2(lines.Sum(x => x.Quantity)),
            amount = FinanceBalanceService.Round2(lines.Sum(x => x.Amount)),
            cartons = FinanceBalanceService.Round2(lines.Sum(x => x.Cartons)),
            cbm = FinanceBalanceService.Round2(lines.Sum(x => x.TotalCbm)),
            gwKg = FinanceBalanceService.Round2(lines.Sum(x => x.TotalGwKg)),
            nwKg = FinanceBalanceService.Round2(lines.Sum(x => x.TotalNwKg))
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
        await RefreshShipmentAsync(input.DocumentType, input.DocumentId);
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
        await RefreshShipmentAsync(entity.DocumentType, entity.DocumentId);
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
        await RefreshShipmentAsync(entity.DocumentType, entity.DocumentId);
        return Ok(new { ok = true });
    }

    private async Task RefreshShipmentAsync(string documentType, long documentId)
    {
        if (documentType == "SHP" && await _db.Shipments.AnyAsync(x => x.Id == documentId))
            await _measurementService.RecalculateAsync(documentId, false);
    }

    private static void Calculate(DocumentLine line)
    {
        line.Quantity = FinanceBalanceService.Round2(line.Quantity);
        line.UnitPrice = FinanceBalanceService.Round2(line.UnitPrice);
        line.CartonQty = FinanceBalanceService.Round2(line.CartonQty);
        line.Cartons = FinanceBalanceService.Round2(line.Cartons);
        line.CartonLengthCm = FinanceBalanceService.Round2(line.CartonLengthCm);
        line.CartonWidthCm = FinanceBalanceService.Round2(line.CartonWidthCm);
        line.CartonHeightCm = FinanceBalanceService.Round2(line.CartonHeightCm);
        line.CartonGwKg = FinanceBalanceService.Round2(line.CartonGwKg);
        line.CartonNwKg = FinanceBalanceService.Round2(line.CartonNwKg);

        if (line.CartonQty > 0 && line.Cartons > 0 && line.Quantity <= 0)
            line.Quantity = FinanceBalanceService.Round2(line.CartonQty * line.Cartons);
        if (line.CartonQty > 0 && line.Cartons <= 0 && line.Quantity > 0)
            line.Cartons = FinanceBalanceService.Round2(Math.Ceiling(line.Quantity / line.CartonQty));

        line.Amount = FinanceBalanceService.Round2(line.Quantity * line.UnitPrice);
        line.CartonCbm = FinanceBalanceService.Round2(line.CartonLengthCm * line.CartonWidthCm * line.CartonHeightCm / 1000000m);
        line.TotalCbm = FinanceBalanceService.Round2(line.CartonCbm * line.Cartons);
        line.TotalGwKg = FinanceBalanceService.Round2(line.CartonGwKg * line.Cartons);
        line.TotalNwKg = FinanceBalanceService.Round2(line.CartonNwKg * line.Cartons);
    }
}
