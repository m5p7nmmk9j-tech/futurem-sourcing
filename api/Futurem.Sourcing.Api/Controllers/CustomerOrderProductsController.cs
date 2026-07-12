using Futurem.Sourcing.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/customer-orders/{orderId:long}/products")]
public class CustomerOrderProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomerOrderProductsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List(long orderId)
    {
        var order = await _db.CustomerOrders.FindAsync(orderId);
        if (order is null) return NotFound();

        var products = await _db.OrderProducts
            .Where(x => x.SourceCustomerOrderId == orderId)
            .OrderBy(x => x.Id)
            .ToListAsync();
        var productIds = products.Select(x => x.Id).ToList();
        var images = await _db.OrderProductImages
            .Where(x => productIds.Contains(x.OrderProductId))
            .OrderBy(x => x.SortNo)
            .ToListAsync();
        var lines = await _db.DocumentLines
            .Where(x => !x.IsDeleted && x.DocumentType == "CO" && x.DocumentId == orderId &&
                        x.OrderProductId.HasValue && productIds.Contains(x.OrderProductId.Value))
            .ToListAsync();

        var imagesByProduct = images.GroupBy(x => x.OrderProductId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var lineByProduct = lines.GroupBy(x => x.OrderProductId!.Value)
            .ToDictionary(x => x.Key, x => x.First());

        return Ok(products.Select(product => new
        {
            product,
            images = imagesByProduct.GetValueOrDefault(product.Id) ?? [],
            line = lineByProduct.GetValueOrDefault(product.Id)
        }));
    }
}
