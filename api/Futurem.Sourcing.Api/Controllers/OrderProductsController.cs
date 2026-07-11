using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/order-products")]
public class OrderProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public OrderProductsController(AppDbContext db)
    {
        _db = db;
    }

    public sealed record ImageInput(
        string ImageUrl,
        string ImageType = "detail",
        int SortNo = 0,
        string? FileName = null,
        string? ContentType = null);

    public sealed record DraftRequest(
        long CustomerOrderId,
        long SupplierId,
        string? SystemSku,
        string? CustomerItemNo,
        string? CustomerBarcode,
        string? SupplierItemNo,
        string NameCn,
        string? NameEn,
        string? NameEs,
        string? Specification,
        string? Color,
        string? Unit,
        decimal PurchaseUnitPrice,
        decimal SalesUnitPrice,
        decimal Quantity,
        decimal Cartons,
        decimal CartonQty,
        decimal CartonLengthCm,
        decimal CartonWidthCm,
        decimal CartonHeightCm,
        decimal CartonGwKg,
        decimal CartonNwKg,
        IReadOnlyCollection<ImageInput>? Images,
        string? Remark);

    public sealed record CopyRequest(long TargetCustomerOrderId, long SupplierId);

    [HttpGet("history")]
    public async Task<IActionResult> History(
        [FromQuery] long? customerId,
        [FromQuery] long? supplierId,
        [FromQuery] string? keyword)
    {
        var query = _db.OrderProducts.AsQueryable();
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(x =>
                x.SystemSku.Contains(term) ||
                (x.CustomerItemNo != null && x.CustomerItemNo.Contains(term)) ||
                x.CustomerBarcode.Contains(term) ||
                x.NameCn.Contains(term) ||
                (x.NameEn != null && x.NameEn.Contains(term)));
        }

        var products = await query
            .OrderByDescending(x => x.Id)
            .Take(500)
            .ToListAsync();
        var ids = products.Select(x => x.Id).ToList();
        var mainImages = await _db.OrderProductImages
            .Where(x => ids.Contains(x.OrderProductId) && x.ImageType == "main")
            .OrderBy(x => x.SortNo)
            .ToListAsync();
        var imageByProduct = mainImages
            .GroupBy(x => x.OrderProductId)
            .ToDictionary(x => x.Key, x => x.First().ImageUrl);

        return Ok(products.Select(x => new
        {
            product = x,
            mainImageUrl = imageByProduct.GetValueOrDefault(x.Id)
        }));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        var product = await _db.OrderProducts.FindAsync(id);
        if (product is null) return NotFound();
        var images = await _db.OrderProductImages
            .Where(x => x.OrderProductId == id)
            .OrderBy(x => x.SortNo)
            .ToListAsync();
        var line = await _db.DocumentLines
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.DocumentType == "CO" &&
                                      x.DocumentId == product.SourceCustomerOrderId &&
                                      x.OrderProductId == product.Id);
        return Ok(new { product, images, line });
    }

    [HttpPost]
    public async Task<ActionResult<OrderProduct>> Create(DraftRequest request)
    {
        var order = await RequireDraftOrderAsync(request.CustomerOrderId);
        ValidateDraft(request);
        var sku = string.IsNullOrWhiteSpace(request.SystemSku)
            ? NumberService.NewProductSku()
            : request.SystemSku.Trim();
        var barcode = FirstNonEmpty(request.CustomerBarcode, request.CustomerItemNo, sku);
        await EnsureBarcodeAvailableAsync(order.Id, barcode, null);

        var product = new OrderProduct
        {
            CustomerId = order.CustomerId,
            SupplierId = request.SupplierId,
            SourceCustomerOrderId = order.Id,
            SystemSku = sku,
            CustomerItemNo = request.CustomerItemNo?.Trim(),
            CustomerBarcode = barcode,
            SupplierItemNo = request.SupplierItemNo?.Trim(),
            NameCn = request.NameCn.Trim(),
            NameEn = request.NameEn?.Trim(),
            NameEs = request.NameEs?.Trim(),
            Specification = request.Specification?.Trim(),
            Color = request.Color?.Trim(),
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? "PCS" : request.Unit.Trim(),
            PurchaseUnitPrice = RmbMoneyService.Round(request.PurchaseUnitPrice),
            SalesUnitPrice = RmbMoneyService.Round(request.SalesUnitPrice),
            CartonQty = request.CartonQty,
            CartonLengthCm = request.CartonLengthCm,
            CartonWidthCm = request.CartonWidthCm,
            CartonHeightCm = request.CartonHeightCm,
            CartonCbm = CalculateCartonCbm(request.CartonLengthCm, request.CartonWidthCm, request.CartonHeightCm),
            CartonGwKg = request.CartonGwKg,
            CartonNwKg = request.CartonNwKg,
            ImporterProfileId = order.ImporterProfileId ?? 0,
            ImporterSnapshotJson = order.ImporterSnapshotJson,
            LabelTemplateId = order.LabelTemplateId ?? 0,
            LabelTemplateSnapshotJson = order.LabelTemplateSnapshotJson,
            MarkTemplateId = order.MarkTemplateId ?? 0,
            MarkTemplateSnapshotJson = order.MarkTemplateSnapshotJson,
            BatchCode = (order.OrderDate ?? DateTime.Today).ToString("yyyyMMdd"),
            Status = "draft",
            Remark = request.Remark,
            CreatedAt = DateTime.Now
        };
        _db.OrderProducts.Add(product);
        await _db.SaveChangesAsync();

        AddImages(product.Id, request.Images);
        _db.DocumentLines.Add(BuildCoLine(order, product, request.Quantity, request.Cartons));
        await _db.SaveChangesAsync();
        return product;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<OrderProduct>> Update(long id, DraftRequest request)
    {
        var product = await _db.OrderProducts.FindAsync(id);
        if (product is null) return NotFound();
        if (product.SourceCustomerOrderId != request.CustomerOrderId)
            throw new BusinessRuleException("ORDER_PRODUCT_ORDER_IMMUTABLE", "订单商品所属客户订单不能修改");
        var order = await RequireDraftOrderAsync(product.SourceCustomerOrderId);
        ValidateDraft(request);

        var sku = string.IsNullOrWhiteSpace(request.SystemSku)
            ? product.SystemSku
            : request.SystemSku.Trim();
        var barcode = FirstNonEmpty(request.CustomerBarcode, request.CustomerItemNo, sku);
        await EnsureBarcodeAvailableAsync(order.Id, barcode, product.Id);

        product.SupplierId = request.SupplierId;
        product.SystemSku = sku;
        product.CustomerItemNo = request.CustomerItemNo?.Trim();
        product.CustomerBarcode = barcode;
        product.SupplierItemNo = request.SupplierItemNo?.Trim();
        product.NameCn = request.NameCn.Trim();
        product.NameEn = request.NameEn?.Trim();
        product.NameEs = request.NameEs?.Trim();
        product.Specification = request.Specification?.Trim();
        product.Color = request.Color?.Trim();
        product.Unit = string.IsNullOrWhiteSpace(request.Unit) ? "PCS" : request.Unit.Trim();
        product.PurchaseUnitPrice = RmbMoneyService.Round(request.PurchaseUnitPrice);
        product.SalesUnitPrice = RmbMoneyService.Round(request.SalesUnitPrice);
        product.CartonQty = request.CartonQty;
        product.CartonLengthCm = request.CartonLengthCm;
        product.CartonWidthCm = request.CartonWidthCm;
        product.CartonHeightCm = request.CartonHeightCm;
        product.CartonCbm = CalculateCartonCbm(request.CartonLengthCm, request.CartonWidthCm, request.CartonHeightCm);
        product.CartonGwKg = request.CartonGwKg;
        product.CartonNwKg = request.CartonNwKg;
        product.Remark = request.Remark;
        product.UpdatedAt = DateTime.Now;

        var line = await _db.DocumentLines.FirstOrDefaultAsync(x =>
            !x.IsDeleted && x.DocumentType == "CO" && x.DocumentId == order.Id && x.OrderProductId == product.Id);
        if (line is null)
            _db.DocumentLines.Add(BuildCoLine(order, product, request.Quantity, request.Cartons));
        else
            ApplyCoLine(line, order, product, request.Quantity, request.Cartons);

        if (request.Images is not null)
        {
            var oldImages = await _db.OrderProductImages.Where(x => x.OrderProductId == product.Id).ToListAsync();
            foreach (var image in oldImages) image.IsDeleted = true;
            AddImages(product.Id, request.Images);
        }

        await _db.SaveChangesAsync();
        return product;
    }

    [HttpPost("{id:long}/copy-to-order")]
    public async Task<ActionResult<OrderProduct>> CopyToOrder(long id, CopyRequest request)
    {
        var service = new OrderProductService(_db);
        return await service.CopyToOrderAsync(id, request.TargetCustomerOrderId, request.SupplierId);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var product = await _db.OrderProducts.FindAsync(id);
        if (product is null) return NotFound();
        await RequireDraftOrderAsync(product.SourceCustomerOrderId);
        product.IsDeleted = true;
        product.UpdatedAt = DateTime.Now;
        var lines = await _db.DocumentLines.Where(x =>
            !x.IsDeleted && x.DocumentType == "CO" && x.DocumentId == product.SourceCustomerOrderId &&
            x.OrderProductId == product.Id).ToListAsync();
        foreach (var line in lines)
        {
            line.IsDeleted = true;
            line.UpdatedAt = DateTime.Now;
        }
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    private async Task<CustomerOrder> RequireDraftOrderAsync(long orderId)
    {
        var order = await _db.CustomerOrders.FindAsync(orderId)
            ?? throw new KeyNotFoundException("客户订单不存在");
        if (order.Status != "draft")
            throw new BusinessRuleException("ORDER_LOCKED", "客户订单不是草稿，不能修改订单商品");
        return order;
    }

    private async Task EnsureBarcodeAvailableAsync(long orderId, string barcode, long? exceptProductId)
    {
        var exists = await _db.OrderProducts.AnyAsync(x =>
            x.SourceCustomerOrderId == orderId && x.CustomerBarcode == barcode &&
            (!exceptProductId.HasValue || x.Id != exceptProductId.Value));
        if (exists)
            throw new BusinessRuleException("CUSTOMER_BARCODE_DUPLICATED", "同一客户订单内客户条码不能重复");
    }

    private static void ValidateDraft(DraftRequest request)
    {
        if (request.SupplierId <= 0)
            throw new BusinessRuleException("SUPPLIER_REQUIRED", "请选择商品供应商");
        if (string.IsNullOrWhiteSpace(request.NameCn))
            throw new BusinessRuleException("PRODUCT_NAME_REQUIRED", "商品名称不能为空");
        if (request.Quantity <= 0 || request.Cartons <= 0 || request.CartonQty <= 0)
            throw new BusinessRuleException("PACKING_REQUIRED", "数量、箱数和单箱数量必须大于零");
    }

    private void AddImages(long productId, IReadOnlyCollection<ImageInput>? images)
    {
        if (images is null) return;
        var mainSeen = false;
        foreach (var input in images.OrderBy(x => x.SortNo))
        {
            if (string.IsNullOrWhiteSpace(input.ImageUrl)) continue;
            var imageType = string.IsNullOrWhiteSpace(input.ImageType) ? "detail" : input.ImageType.Trim();
            if (imageType == "main")
            {
                if (mainSeen) imageType = "detail";
                else mainSeen = true;
            }
            _db.OrderProductImages.Add(new OrderProductImage
            {
                OrderProductId = productId,
                ImageUrl = input.ImageUrl.Trim(),
                ImageType = imageType,
                SortNo = input.SortNo,
                FileName = input.FileName,
                ContentType = input.ContentType,
                CreatedAt = DateTime.Now
            });
        }
    }

    private static DocumentLine BuildCoLine(
        CustomerOrder order,
        OrderProduct product,
        decimal quantity,
        decimal cartons)
    {
        var line = new DocumentLine
        {
            DocumentType = "CO",
            DocumentId = order.Id,
            OrderProductId = product.Id,
            SortNo = 0,
            CreatedAt = DateTime.Now
        };
        ApplyCoLine(line, order, product, quantity, cartons);
        return line;
    }

    private static void ApplyCoLine(
        DocumentLine line,
        CustomerOrder order,
        OrderProduct product,
        decimal quantity,
        decimal cartons)
    {
        line.CustomerId = order.CustomerId;
        line.SupplierId = product.SupplierId;
        line.Sku = product.SystemSku;
        line.ProductName = product.NameCn;
        line.Unit = product.Unit;
        line.Quantity = quantity;
        line.UnitPrice = product.SalesUnitPrice;
        line.Amount = RmbMoneyService.Round(quantity * product.SalesUnitPrice);
        line.CartonQty = product.CartonQty;
        line.Cartons = cartons;
        line.CartonLengthCm = product.CartonLengthCm;
        line.CartonWidthCm = product.CartonWidthCm;
        line.CartonHeightCm = product.CartonHeightCm;
        line.CartonCbm = product.CartonCbm;
        line.TotalCbm = RmbMoneyService.Round(product.CartonCbm * cartons);
        line.CartonGwKg = product.CartonGwKg;
        line.TotalGwKg = RmbMoneyService.Round(product.CartonGwKg * cartons);
        line.CartonNwKg = product.CartonNwKg;
        line.TotalNwKg = RmbMoneyService.Round(product.CartonNwKg * cartons);
        line.SupplierItemNo = product.SupplierItemNo;
        line.CustomerItemNo = product.CustomerItemNo;
        line.PurchaseUnitPriceSnapshot = product.PurchaseUnitPrice;
        line.SalesUnitPriceSnapshot = product.SalesUnitPrice;
        line.UpdatedAt = DateTime.Now;
    }

    private static decimal CalculateCartonCbm(decimal length, decimal width, decimal height)
        => length > 0 && width > 0 && height > 0
            ? RmbMoneyService.Round(length * width * height / 1_000_000m)
            : 0m;

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim()
           ?? throw new BusinessRuleException("CUSTOMER_BARCODE_REQUIRED", "客户条码或系统 SKU 不能为空");
}
