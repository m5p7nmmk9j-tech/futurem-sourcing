using System.Security.Claims;
using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly InventoryService _service;

    public InventoryController(AppDbContext db, InventoryService service)
    {
        _db = db;
        _service = service;
    }

    public sealed record AdjustRequest(decimal QuantityDelta, decimal CartonsDelta, string Reason);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] long? customerId,
        [FromQuery] long? warehouseId,
        [FromQuery] long? supplierId,
        [FromQuery] long? summaryOrderId,
        [FromQuery] long? receivingOrderId,
        [FromQuery] long? locationId,
        [FromQuery] string? keyword,
        [FromQuery] string? purchaseOrderNo,
        [FromQuery] string? lotNo,
        [FromQuery] string? status)
    {
        var query = _db.InventoryLots.AsQueryable();
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        if (warehouseId.HasValue) query = query.Where(x => x.WarehouseId == warehouseId.Value);
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value);
        if (summaryOrderId.HasValue) query = query.Where(x => x.SummaryOrderId == summaryOrderId.Value);
        if (receivingOrderId.HasValue) query = query.Where(x => x.ReceivingOrderId == receivingOrderId.Value);
        if (locationId.HasValue) query = query.Where(x => x.WarehouseLocationId == locationId.Value);
        if (!string.IsNullOrWhiteSpace(lotNo)) query = query.Where(x => x.LotNo.Contains(lotNo.Trim()));
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());

        var lots = await query.OrderByDescending(x => x.Id).Take(500).ToListAsync();
        var productIds = lots.Select(x => x.OrderProductId).Distinct().ToList();
        var poIds = lots.Select(x => x.PurchaseOrderId).Distinct().ToList();
        var warehouseIds = lots.Select(x => x.WarehouseId).Distinct().ToList();
        var locationIds = lots.Where(x => x.WarehouseLocationId.HasValue).Select(x => x.WarehouseLocationId!.Value).Distinct().ToList();
        var customerIds = lots.Select(x => x.CustomerId).Distinct().ToList();
        var supplierIds = lots.Select(x => x.SupplierId).Distinct().ToList();
        var receivingIds = lots.Select(x => x.ReceivingOrderId).Distinct().ToList();
        var summaryIds = lots.Where(x => x.SummaryOrderId.HasValue).Select(x => x.SummaryOrderId!.Value).Distinct().ToList();

        var products = await _db.OrderProducts.Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var purchaseOrders = await _db.PurchaseOrders.Where(x => poIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var warehouses = await _db.Warehouses.Where(x => warehouseIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var locations = await _db.WarehouseLocations.Where(x => locationIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var customers = await _db.Customers.Where(x => customerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var suppliers = await _db.Suppliers.Where(x => supplierIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var receivings = await _db.ReceivingOrders.Where(x => receivingIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var summaries = await _db.SummaryOrders.Where(x => summaryIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

        var term = keyword?.Trim().ToLowerInvariant();
        var poTerm = purchaseOrderNo?.Trim().ToLowerInvariant();
        var rows = lots.Select(lot =>
        {
            products.TryGetValue(lot.OrderProductId, out var product);
            purchaseOrders.TryGetValue(lot.PurchaseOrderId, out var po);
            warehouses.TryGetValue(lot.WarehouseId, out var warehouse);
            if (lot.WarehouseLocationId.HasValue) locations.TryGetValue(lot.WarehouseLocationId.Value, out var location);
            else location = null;
            customers.TryGetValue(lot.CustomerId, out var customer);
            suppliers.TryGetValue(lot.SupplierId, out var supplier);
            receivings.TryGetValue(lot.ReceivingOrderId, out var receiving);
            if (lot.SummaryOrderId.HasValue) summaries.TryGetValue(lot.SummaryOrderId.Value, out var summary);
            else summary = null;
            return new { lot, product, purchaseOrder = po, warehouse, location, customer, supplier, receiving, summary };
        });

        if (!string.IsNullOrWhiteSpace(term))
        {
            rows = rows.Where(x => new[]
            {
                x.lot.LotNo,
                x.product?.CustomerItemNo,
                x.product?.CustomerBarcode,
                x.product?.SystemSku,
                x.product?.NameCn,
                x.purchaseOrder?.No,
                x.receiving?.No,
                x.summary?.No,
                x.location?.Code
            }.Any(value => (value ?? string.Empty).ToLowerInvariant().Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(poTerm))
            rows = rows.Where(x => (x.purchaseOrder?.No ?? string.Empty).ToLowerInvariant().Contains(poTerm));

        return Ok(rows.ToList());
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        var lot = await _db.InventoryLots.FindAsync(id);
        if (lot is null) return NotFound();
        var transactions = await _db.InventoryTransactions
            .Where(x => x.InventoryLotId == id)
            .OrderByDescending(x => x.Id)
            .ToListAsync();
        return Ok(new { lot, transactions });
    }

    [HttpGet("{id:long}/availability")]
    public async Task<IActionResult> Availability(long id)
        => Ok(await _service.GetAvailableAsync(id));

    [HttpPost("{id:long}/adjust")]
    public async Task<IActionResult> Adjust(long id, AdjustRequest request)
        => Ok(await _service.AdjustAsync(id, request.QuantityDelta, request.CartonsDelta, request.Reason, CurrentUserId()));

    private long? CurrentUserId()
        => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
