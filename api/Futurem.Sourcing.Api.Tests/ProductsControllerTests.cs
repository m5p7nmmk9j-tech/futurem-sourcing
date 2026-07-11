using Futurem.Sourcing.Api.Controllers;
using Futurem.Sourcing.Api.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Futurem.Sourcing.Api.Tests;

public class ProductsControllerTests
{
    [Fact]
    public async Task Update_AllowsUniqueBarcodeAndRejectsDeletedProductBarcode()
    {
        await using var db = TestDbFactory.Create();
        var active = new Product { Sku = "SKU-1", Barcode = "111", NameCn = "商品A", Unit = "PCS" };
        var deleted = new Product { Sku = "SKU-2", Barcode = "222", NameCn = "商品B", Unit = "PCS", IsDeleted = true };
        db.Products.AddRange(active, deleted);
        await db.SaveChangesAsync();
        var controller = new ProductsController(db);

        var ok = await controller.Update(active.Id, new Product
        {
            Barcode = "333",
            NameCn = "商品A",
            Unit = "PCS",
            PurchasePrice = 12.345m,
            CartonGwKg = 10.555m,
            CartonNwKg = 9.444m
        });

        Assert.Equal("333", ok.Value!.Barcode);
        Assert.Equal(12.35m, ok.Value.PurchasePrice);
        Assert.Equal(10.56m, ok.Value.CartonGwKg);
        Assert.Equal(9.44m, ok.Value.CartonNwKg);

        var duplicate = await controller.Update(active.Id, new Product
        {
            Barcode = "222",
            NameCn = "商品A",
            Unit = "PCS"
        });

        Assert.IsType<BadRequestObjectResult>(duplicate.Result);
    }
}
