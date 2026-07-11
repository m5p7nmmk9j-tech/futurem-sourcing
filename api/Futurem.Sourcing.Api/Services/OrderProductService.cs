using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public sealed class OrderProductService
{
    private readonly AppDbContext _db;

    public OrderProductService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<OrderProduct> CopyToOrderAsync(
        long sourceOrderProductId,
        long targetCustomerOrderId,
        long supplierId)
    {
        var source = await _db.OrderProducts
            .FirstOrDefaultAsync(x => x.Id == sourceOrderProductId)
            ?? throw new KeyNotFoundException("订单商品不存在");
        var targetOrder = await _db.CustomerOrders
            .FirstOrDefaultAsync(x => x.Id == targetCustomerOrderId)
            ?? throw new KeyNotFoundException("目标客户订单不存在");

        if (targetOrder.Status != "draft")
            throw new BusinessRuleException("ORDER_LOCKED", "目标客户订单不是草稿，不能添加历史商品");
        if (targetOrder.CustomerId != source.CustomerId)
            throw new BusinessRuleException("ORDER_PRODUCT_CUSTOMER_MISMATCH", "历史商品与目标订单客户不一致");

        var effectiveSupplierId = supplierId > 0 ? supplierId : source.SupplierId;
        var copy = new OrderProduct
        {
            CustomerId = targetOrder.CustomerId,
            SupplierId = effectiveSupplierId,
            SourceOrderProductId = source.Id,
            SourceCustomerOrderId = targetOrder.Id,
            SystemSku = source.SystemSku,
            CustomerItemNo = source.CustomerItemNo,
            CustomerBarcode = source.CustomerBarcode,
            SupplierItemNo = source.SupplierItemNo,
            NameCn = source.NameCn,
            NameEn = source.NameEn,
            NameEs = source.NameEs,
            Specification = source.Specification,
            Color = source.Color,
            Unit = source.Unit,
            PurchaseUnitPrice = source.PurchaseUnitPrice,
            SalesUnitPrice = source.SalesUnitPrice,
            CartonQty = source.CartonQty,
            CartonLengthCm = source.CartonLengthCm,
            CartonWidthCm = source.CartonWidthCm,
            CartonHeightCm = source.CartonHeightCm,
            CartonCbm = source.CartonCbm,
            CartonGwKg = source.CartonGwKg,
            CartonNwKg = source.CartonNwKg,
            ImporterProfileId = targetOrder.ImporterProfileId ?? source.ImporterProfileId,
            ImporterSnapshotJson = PreferTargetSnapshot(targetOrder.ImporterSnapshotJson, source.ImporterSnapshotJson),
            LabelTemplateId = targetOrder.LabelTemplateId ?? source.LabelTemplateId,
            LabelTemplateSnapshotJson = PreferTargetSnapshot(targetOrder.LabelTemplateSnapshotJson, source.LabelTemplateSnapshotJson),
            MarkTemplateId = targetOrder.MarkTemplateId ?? source.MarkTemplateId,
            MarkTemplateSnapshotJson = PreferTargetSnapshot(targetOrder.MarkTemplateSnapshotJson, source.MarkTemplateSnapshotJson),
            BatchCode = DateTime.Today.ToString("yyyyMMdd"),
            Status = "draft",
            LockedAt = null,
            NeedsReview = source.NeedsReview,
            Remark = $"复制自订单商品 {source.Id}",
            CreatedAt = DateTime.Now
        };

        _db.OrderProducts.Add(copy);
        await _db.SaveChangesAsync();

        var sourceImages = await _db.OrderProductImages
            .Where(x => x.OrderProductId == source.Id)
            .OrderBy(x => x.SortNo)
            .ToListAsync();
        foreach (var image in sourceImages)
        {
            _db.OrderProductImages.Add(new OrderProductImage
            {
                OrderProductId = copy.Id,
                ImageUrl = image.ImageUrl,
                ImageType = image.ImageType,
                SortNo = image.SortNo,
                FileName = image.FileName,
                ContentType = image.ContentType,
                Remark = image.Remark,
                CreatedAt = DateTime.Now
            });
        }

        var sourceLines = await _db.DocumentLines
            .Where(x => !x.IsDeleted && x.DocumentType == "CO" &&
                        x.DocumentId == source.SourceCustomerOrderId &&
                        x.OrderProductId == source.Id)
            .OrderBy(x => x.SortNo)
            .ToListAsync();
        foreach (var sourceLine in sourceLines)
            _db.DocumentLines.Add(CloneLineForOrder(sourceLine, targetOrder, copy));

        await _db.SaveChangesAsync();
        return copy;
    }

    private static string PreferTargetSnapshot(string? target, string source)
        => string.IsNullOrWhiteSpace(target) || target.Trim() == "{}" ? source : target;

    private static DocumentLine CloneLineForOrder(
        DocumentLine source,
        CustomerOrder targetOrder,
        OrderProduct product)
    {
        var amount = RmbMoneyService.Round(source.Quantity * product.SalesUnitPrice);
        return new DocumentLine
        {
            DocumentType = "CO",
            DocumentId = targetOrder.Id,
            ProductId = source.ProductId,
            OrderProductId = product.Id,
            SourceDocumentLineId = source.Id,
            CustomerId = targetOrder.CustomerId,
            SupplierId = product.SupplierId,
            Sku = product.SystemSku,
            ProductName = product.NameCn,
            Unit = product.Unit,
            Quantity = source.Quantity,
            UnitPrice = product.SalesUnitPrice,
            Amount = amount,
            CartonQty = product.CartonQty,
            Cartons = source.Cartons,
            CartonLengthCm = product.CartonLengthCm,
            CartonWidthCm = product.CartonWidthCm,
            CartonHeightCm = product.CartonHeightCm,
            CartonCbm = product.CartonCbm,
            TotalCbm = RmbMoneyService.Round(product.CartonCbm * source.Cartons),
            CartonGwKg = product.CartonGwKg,
            TotalGwKg = RmbMoneyService.Round(product.CartonGwKg * source.Cartons),
            CartonNwKg = product.CartonNwKg,
            TotalNwKg = RmbMoneyService.Round(product.CartonNwKg * source.Cartons),
            SupplierItemNo = product.SupplierItemNo,
            CustomerItemNo = product.CustomerItemNo,
            PurchaseUnitPriceSnapshot = product.PurchaseUnitPrice,
            SalesUnitPriceSnapshot = product.SalesUnitPrice,
            SortNo = source.SortNo,
            Remark = source.Remark,
            CreatedAt = DateTime.Now
        };
    }
}
