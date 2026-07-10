using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/customer-orders")]
public class CustomerOrdersController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomerOrdersController(AppDbContext db)
    {
        _db = db;
    }

    public record GeneratePoRequest(long SupplierId, DateTime? ExpectedDeliveryDate, string? Currency, string? DeliveryTerms, string? PaymentTerms);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] long? customerId)
    {
        var query = _db.CustomerOrders.AsQueryable();
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        var orders = await query.OrderByDescending(x => x.Id).Take(200).ToListAsync();
        var ids = orders.Select(x => x.Id).ToList();
        var lines = await _db.DocumentLines
            .Where(x => !x.IsDeleted && x.DocumentType == "CO" && ids.Contains(x.DocumentId))
            .ToListAsync();
        var summaries = lines
            .GroupBy(x => x.DocumentId)
            .ToDictionary(
                x => x.Key,
                x => new
                {
                    TotalCbm = x.Sum(CalculateTotalCbm),
                    Cartons = x.Sum(line => line.Cartons),
                    Quantity = x.Sum(line => line.Quantity)
                });
        return Ok(orders.Select(x => new
        {
            x.Id,
            x.No,
            x.BuyingTripId,
            x.CustomerId,
            x.RfqId,
            x.OrderDate,
            x.ExpectedDeliveryDate,
            x.Currency,
            x.Status,
            x.DeliveryTerms,
            x.PaymentTerms,
            x.Remark,
            totalCbm = summaries.TryGetValue(x.Id, out var s) ? s.TotalCbm : 0,
            cartons = summaries.TryGetValue(x.Id, out s) ? s.Cartons : 0,
            quantity = summaries.TryGetValue(x.Id, out s) ? s.Quantity : 0
        }));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<CustomerOrder>> Get(long id)
    {
        var entity = await _db.CustomerOrders.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<CustomerOrder>> Create(CustomerOrder input)
    {
        input.Id = 0;
        input.No = string.IsNullOrWhiteSpace(input.No) ? NumberService.NewNo("CO") : input.No;
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "draft" : input.Status;
        input.CreatedAt = DateTime.Now;
        _db.CustomerOrders.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPost("{id:long}/copy")]
    public async Task<ActionResult<CustomerOrder>> Copy(long id)
    {
        var source = await _db.CustomerOrders.FindAsync(id);
        if (source == null) return NotFound();

        var copy = new CustomerOrder
        {
            No = NumberService.NewNo("CO"),
            BuyingTripId = source.BuyingTripId,
            CustomerId = source.CustomerId,
            RfqId = source.RfqId,
            OrderDate = DateTime.Today,
            ExpectedDeliveryDate = source.ExpectedDeliveryDate,
            Currency = source.Currency,
            Status = "draft",
            DeliveryTerms = source.DeliveryTerms,
            PaymentTerms = source.PaymentTerms,
            Remark = $"复制自 {source.No}",
            CreatedAt = DateTime.Now
        };
        _db.CustomerOrders.Add(copy);
        await _db.SaveChangesAsync();
        await DocumentLineCopyService.CopyAsync(_db, "CO", source.Id, "CO", copy.Id);
        await _db.SaveChangesAsync();
        return copy;
    }

    [HttpPost("{id:long}/generate-po")]
    public async Task<ActionResult<PurchaseOrder>> GeneratePo(long id, GeneratePoRequest request)
    {
        var source = await _db.CustomerOrders.FindAsync(id);
        if (source == null) return NotFound();
        if (request.SupplierId <= 0) return BadRequest("SupplierId required");

        var po = new PurchaseOrder
        {
            No = NumberService.NewNo("PO"),
            BuyingTripId = source.BuyingTripId,
            CustomerOrderId = source.Id,
            SupplierId = request.SupplierId,
            CustomerId = source.CustomerId,
            OrderDate = DateTime.Today,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "RMB" : request.Currency!,
            Status = "draft",
            PayStatus = "unpaid",
            DeliveryTerms = string.IsNullOrWhiteSpace(request.DeliveryTerms) ? source.DeliveryTerms : request.DeliveryTerms,
            PaymentTerms = string.IsNullOrWhiteSpace(request.PaymentTerms) ? source.PaymentTerms : request.PaymentTerms,
            Remark = $"由 CO {source.No} 生成",
            CreatedAt = DateTime.Now
        };
        _db.PurchaseOrders.Add(po);
        source.Status = "converted";
        source.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        await DocumentLineCopyService.CopyAsync(_db, "CO", source.Id, "PO", po.Id);
        await _db.SaveChangesAsync();
        return po;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<CustomerOrder>> Update(long id, CustomerOrder input)
    {
        var entity = await _db.CustomerOrders.FindAsync(id);
        if (entity == null) return NotFound();

        entity.CustomerId = input.CustomerId;
        entity.BuyingTripId = input.BuyingTripId;
        entity.RfqId = input.RfqId;
        entity.OrderDate = input.OrderDate;
        entity.ExpectedDeliveryDate = input.ExpectedDeliveryDate;
        entity.Currency = input.Currency;
        entity.Status = input.Status;
        entity.DeliveryTerms = input.DeliveryTerms;
        entity.PaymentTerms = input.PaymentTerms;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.CustomerOrders.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    private static decimal CalculateTotalCbm(DocumentLine line)
    {
        var cartonCbm = line.CartonCbm;
        if (cartonCbm <= 0 && line.CartonLengthCm > 0 && line.CartonWidthCm > 0 && line.CartonHeightCm > 0)
        {
            cartonCbm = line.CartonLengthCm * line.CartonWidthCm * line.CartonHeightCm / 1_000_000m;
        }

        if (line.TotalCbm > 0) return line.TotalCbm;
        return cartonCbm * line.Cartons;
    }
}
