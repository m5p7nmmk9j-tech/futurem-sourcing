using System.Text.Json;
using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Futurem.Sourcing.Api.Services;

public sealed record GeneratePoItem(long OrderProductId, decimal Quantity);

public sealed class CustomerOrderWorkflowService
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly AuditTrailService _audit;

    public CustomerOrderWorkflowService(AppDbContext db, AuditTrailService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<CustomerOrder> ConfirmAsync(
        long orderId,
        string reason,
        long? userId)
    {
        var order = await _db.CustomerOrders.FirstOrDefaultAsync(x => x.Id == orderId)
            ?? throw new KeyNotFoundException("客户订单不存在");
        if (order.Status is "confirmed" or "partially_converted" or "converted") return order;
        if (order.Status != "draft")
            throw new BusinessRuleException("ORDER_LOCKED", "只有草稿客户订单可以确认");

        await using var transaction = await BeginTransactionIfSupportedAsync();
        try
        {
            var importer = await RequireImporterAsync(order);
            var label = await RequireTemplateAsync(order, order.LabelTemplateId, "product_label", "商品标签模板");
            var mark = await RequireTemplateAsync(order, order.MarkTemplateId, "carton_mark", "外箱唛头模板");

            var lines = await _db.DocumentLines
                .Where(x => !x.IsDeleted && x.DocumentType == "CO" && x.DocumentId == order.Id)
                .OrderBy(x => x.SortNo)
                .ToListAsync();
            if (lines.Count == 0)
                throw new BusinessRuleException("ORDER_PRODUCTS_REQUIRED", "客户订单至少需要一个商品");

            var productIds = lines
                .Where(x => x.OrderProductId.HasValue)
                .Select(x => x.OrderProductId!.Value)
                .Distinct()
                .ToList();
            if (productIds.Count != lines.Count)
                throw new BusinessRuleException("ORDER_PRODUCT_LINK_REQUIRED", "所有订单明细必须关联订单商品");

            var products = await _db.OrderProducts
                .Where(x => productIds.Contains(x.Id) && x.SourceCustomerOrderId == order.Id)
                .ToListAsync();
            if (products.Count != productIds.Count)
                throw new BusinessRuleException("ORDER_PRODUCT_LINK_INVALID", "订单商品来源不完整");

            var importerSnapshot = JsonSerializer.Serialize(importer, SnapshotJsonOptions);
            var labelSnapshot = JsonSerializer.Serialize(label, SnapshotJsonOptions);
            var markSnapshot = JsonSerializer.Serialize(mark, SnapshotJsonOptions);
            var now = DateTime.Now;
            var before = new { order.Status, order.ConfirmedAt };

            order.Currency = RmbMoneyService.Currency;
            order.ImporterSnapshotJson = importerSnapshot;
            order.LabelTemplateSnapshotJson = labelSnapshot;
            order.MarkTemplateSnapshotJson = markSnapshot;
            order.Status = "confirmed";
            order.ConfirmedAt = now;
            order.UpdatedAt = now;

            var linesByProduct = lines.ToDictionary(x => x.OrderProductId!.Value);
            foreach (var product in products)
            {
                ValidateProduct(product, linesByProduct[product.Id]);
                product.ImporterProfileId = importer.Id;
                product.ImporterSnapshotJson = importerSnapshot;
                product.LabelTemplateId = label.Id;
                product.LabelTemplateSnapshotJson = labelSnapshot;
                product.MarkTemplateId = mark.Id;
                product.MarkTemplateSnapshotJson = markSnapshot;
                product.BatchCode = string.IsNullOrWhiteSpace(product.BatchCode)
                    ? (order.OrderDate ?? DateTime.Today).ToString("yyyyMMdd")
                    : product.BatchCode;
                product.Status = "locked";
                product.LockedAt = now;
                product.UpdatedAt = now;

                var line = linesByProduct[product.Id];
                line.CustomerId = order.CustomerId;
                line.SupplierId = product.SupplierId;
                line.Sku = product.SystemSku;
                line.ProductName = product.NameCn;
                line.Unit = product.Unit;
                line.CustomerItemNo = product.CustomerItemNo;
                line.SupplierItemNo = product.SupplierItemNo;
                line.PurchaseUnitPriceSnapshot = RmbMoneyService.Round(product.PurchaseUnitPrice);
                line.SalesUnitPriceSnapshot = RmbMoneyService.Round(product.SalesUnitPrice);
                line.UnitPrice = line.SalesUnitPriceSnapshot;
                line.Amount = RmbMoneyService.Round(line.Quantity * line.UnitPrice);
                line.UpdatedAt = now;
            }

            await _db.SaveChangesAsync();
            await _audit.WriteAsync(
                nameof(CustomerOrder),
                order.Id,
                "confirm",
                before,
                new { order.Status, order.ConfirmedAt },
                reason,
                userId);
            if (transaction is not null) await transaction.CommitAsync();
            return order;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<CustomerOrder> ReopenAsync(
        long orderId,
        string reason,
        long? userId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("REOPEN_REASON_REQUIRED", "退回草稿必须填写原因");

        var order = await _db.CustomerOrders.FirstOrDefaultAsync(x => x.Id == orderId)
            ?? throw new KeyNotFoundException("客户订单不存在");
        if (order.Status == "draft") return order;

        var hasActivePo = await _db.PurchaseOrders
            .AnyAsync(x => x.CustomerOrderId == order.Id && x.Status != "cancelled");
        if (hasActivePo)
            throw new BusinessRuleException(
                "ORDER_HAS_DOWNSTREAM_DOCUMENTS",
                "客户订单已有有效采购订单，不能直接退回草稿");

        await using var transaction = await BeginTransactionIfSupportedAsync();
        try
        {
            var before = new { order.Status, order.ConfirmedAt };
            order.Status = "draft";
            order.ConfirmedAt = null;
            order.UpdatedAt = DateTime.Now;

            var products = await _db.OrderProducts
                .Where(x => x.SourceCustomerOrderId == order.Id)
                .ToListAsync();
            foreach (var product in products)
            {
                product.Status = "draft";
                product.LockedAt = null;
                product.UpdatedAt = DateTime.Now;
            }

            await _db.SaveChangesAsync();
            await _audit.WriteAsync(
                nameof(CustomerOrder),
                order.Id,
                "reopen",
                before,
                new { order.Status, order.ConfirmedAt },
                reason,
                userId);
            if (transaction is not null) await transaction.CommitAsync();
            return order;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<PurchaseOrder> GeneratePoAsync(
        long orderId,
        IReadOnlyCollection<GeneratePoItem> items,
        long supplierId,
        DateTime? expectedDeliveryDate,
        long? userId)
    {
        if (items.Count == 0)
            throw new BusinessRuleException("ORDER_PRODUCTS_REQUIRED", "请选择要生成采购订单的商品");
        if (supplierId <= 0)
            throw new BusinessRuleException("SUPPLIER_REQUIRED", "请选择商品供应商");

        var order = await _db.CustomerOrders.FirstOrDefaultAsync(x => x.Id == orderId)
            ?? throw new KeyNotFoundException("客户订单不存在");
        if (order.Status == "draft")
            throw new BusinessRuleException("ORDER_NOT_CONFIRMED", "客户订单确认后才能生成采购订单");
        if (order.Status == "cancelled")
            throw new BusinessRuleException("ORDER_CANCELLED", "已取消客户订单不能生成采购订单");

        var duplicateRequest = items.GroupBy(x => x.OrderProductId).FirstOrDefault(x => x.Count() > 1);
        if (duplicateRequest is not null)
            throw new BusinessRuleException("ORDER_PRODUCT_DUPLICATED", "同一订单商品不能重复选择");

        await using var transaction = await BeginTransactionIfSupportedAsync();
        try
        {
            var requestedIds = items.Select(x => x.OrderProductId).ToList();
            var products = await _db.OrderProducts
                .Where(x => requestedIds.Contains(x.Id) && x.SourceCustomerOrderId == order.Id)
                .ToListAsync();
            if (products.Count != requestedIds.Count)
                throw new BusinessRuleException("ORDER_PRODUCT_LINK_INVALID", "选择的订单商品不属于该客户订单");
            if (products.Any(x => x.SupplierId != supplierId))
                throw new BusinessRuleException(
                    "ORDER_PRODUCT_SUPPLIER_MISMATCH",
                    "订单商品锁定的供应商与所选供应商不一致");

            var coLines = await _db.DocumentLines
                .Where(x => !x.IsDeleted && x.DocumentType == "CO" &&
                            x.DocumentId == order.Id &&
                            x.OrderProductId.HasValue && requestedIds.Contains(x.OrderProductId.Value))
                .ToListAsync();
            if (coLines.Count != requestedIds.Count)
                throw new BusinessRuleException("ORDER_PRODUCT_LINE_MISSING", "订单商品明细不存在");

            var activePoProductIds = await GetActivePoProductIdsAsync(requestedIds);
            if (activePoProductIds.Count > 0)
                throw new BusinessRuleException(
                    "ORDER_PRODUCT_ALREADY_PURCHASED",
                    "订单商品已经进入有效采购订单",
                    new { orderProductIds = activePoProductIds });

            var requestByProduct = items.ToDictionary(x => x.OrderProductId);
            foreach (var line in coLines)
            {
                var requestedQuantity = requestByProduct[line.OrderProductId!.Value].Quantity;
                if (requestedQuantity != line.Quantity)
                    throw new BusinessRuleException(
                        "ORDER_PRODUCT_MUST_CONVERT_FULL_QUANTITY",
                        "订单商品必须以全部未采购数量进入一张采购订单",
                        new
                        {
                            orderProductId = line.OrderProductId.Value,
                            requestedQuantity,
                            remainingQuantity = line.Quantity
                        });
            }

            var productById = products.ToDictionary(x => x.Id);
            var po = new PurchaseOrder
            {
                No = NumberService.NewNo("PO"),
                BuyingTripId = order.BuyingTripId,
                CustomerOrderId = order.Id,
                SupplierId = supplierId,
                CustomerId = order.CustomerId,
                OrderDate = DateTime.Today,
                ExpectedDeliveryDate = expectedDeliveryDate,
                Currency = RmbMoneyService.Currency,
                Status = "draft",
                PayStatus = "unpaid",
                DeliveryTerms = order.DeliveryTerms,
                PaymentTerms = order.PaymentTerms,
                ImporterProfileId = order.ImporterProfileId,
                LabelTemplateId = order.LabelTemplateId,
                MarkTemplateId = order.MarkTemplateId,
                ImporterSnapshotJson = order.ImporterSnapshotJson,
                LabelTemplateSnapshotJson = order.LabelTemplateSnapshotJson,
                MarkTemplateSnapshotJson = order.MarkTemplateSnapshotJson,
                Remark = $"由 CO {order.No} 生成",
                CreatedAt = DateTime.Now
            };
            _db.PurchaseOrders.Add(po);
            await _db.SaveChangesAsync();

            foreach (var sourceLine in coLines.OrderBy(x => x.SortNo))
            {
                var product = productById[sourceLine.OrderProductId!.Value];
                _db.DocumentLines.Add(CloneLineForPo(sourceLine, po, order, product));
            }
            await _db.SaveChangesAsync();

            var allOrderProductIds = await _db.DocumentLines
                .Where(x => !x.IsDeleted && x.DocumentType == "CO" &&
                            x.DocumentId == order.Id && x.OrderProductId.HasValue)
                .Select(x => x.OrderProductId!.Value)
                .Distinct()
                .ToListAsync();
            var purchasedIds = await GetActivePoProductIdsAsync(allOrderProductIds);
            var previousStatus = order.Status;
            order.Status = purchasedIds.Count == allOrderProductIds.Count
                ? "converted"
                : "partially_converted";
            order.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            await _audit.WriteAsync(
                nameof(CustomerOrder),
                order.Id,
                "generate_po",
                new { status = previousStatus },
                new { order.Status, purchaseOrderId = po.Id, orderProductIds = requestedIds },
                "生成采购订单",
                userId);
            if (transaction is not null) await transaction.CommitAsync();
            return po;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<CustomerImporterProfile> RequireImporterAsync(CustomerOrder order)
    {
        if (!order.ImporterProfileId.HasValue || order.ImporterProfileId.Value <= 0)
            throw new BusinessRuleException("IMPORTER_REQUIRED", "请选择进口商资料");
        var importer = await _db.CustomerImporterProfiles
            .FirstOrDefaultAsync(x => x.Id == order.ImporterProfileId.Value &&
                                      x.CustomerId == order.CustomerId &&
                                      x.Status == "active");
        return importer ?? throw new BusinessRuleException("IMPORTER_INVALID", "进口商资料无效或不属于当前客户");
    }

    private async Task<PrintTemplate> RequireTemplateAsync(
        CustomerOrder order,
        long? templateId,
        string templateType,
        string displayName)
    {
        if (!templateId.HasValue || templateId.Value <= 0)
            throw new BusinessRuleException("PRINT_TEMPLATE_REQUIRED", $"请选择{displayName}");
        var template = await _db.PrintTemplates
            .FirstOrDefaultAsync(x => x.Id == templateId.Value &&
                                      x.CustomerId == order.CustomerId &&
                                      x.TemplateType == templateType &&
                                      x.Status == "active");
        return template ?? throw new BusinessRuleException("PRINT_TEMPLATE_INVALID", $"{displayName}无效或不属于当前客户");
    }

    private static void ValidateProduct(OrderProduct product, DocumentLine line)
    {
        if (product.SupplierId <= 0)
            throw new BusinessRuleException("SUPPLIER_REQUIRED", "订单商品必须选择供应商");
        if (string.IsNullOrWhiteSpace(product.SystemSku))
            throw new BusinessRuleException("SKU_REQUIRED", "订单商品系统 SKU 不能为空");
        if (string.IsNullOrWhiteSpace(product.CustomerBarcode))
            throw new BusinessRuleException("CUSTOMER_BARCODE_REQUIRED", "订单商品客户条码不能为空");
        if (string.IsNullOrWhiteSpace(product.NameCn))
            throw new BusinessRuleException("PRODUCT_NAME_REQUIRED", "订单商品名称不能为空");
        if (product.PurchaseUnitPrice <= 0 || product.SalesUnitPrice <= 0)
            throw new BusinessRuleException("PRODUCT_PRICE_REQUIRED", "订单商品采购价和销售价必须大于零");
        if (product.CartonQty <= 0 || line.Cartons <= 0 || line.Quantity <= 0)
            throw new BusinessRuleException("PACKING_REQUIRED", "订单商品数量、箱数和单箱数量必须大于零");
        if (product.CartonLengthCm <= 0 || product.CartonWidthCm <= 0 || product.CartonHeightCm <= 0)
            throw new BusinessRuleException("CARTON_DIMENSIONS_REQUIRED", "订单商品外箱尺寸必须完整");
    }

    private async Task<HashSet<long>> GetActivePoProductIdsAsync(IReadOnlyCollection<long> productIds)
    {
        if (productIds.Count == 0) return [];
        var poLines = await _db.DocumentLines
            .Where(x => !x.IsDeleted && x.DocumentType == "PO" &&
                        x.OrderProductId.HasValue && productIds.Contains(x.OrderProductId.Value))
            .Select(x => new { x.DocumentId, OrderProductId = x.OrderProductId!.Value })
            .ToListAsync();
        if (poLines.Count == 0) return [];

        var poIds = poLines.Select(x => x.DocumentId).Distinct().ToList();
        var activePoIds = await _db.PurchaseOrders
            .Where(x => poIds.Contains(x.Id) && x.Status != "cancelled")
            .Select(x => x.Id)
            .ToListAsync();
        return poLines
            .Where(x => activePoIds.Contains(x.DocumentId))
            .Select(x => x.OrderProductId)
            .ToHashSet();
    }

    private static DocumentLine CloneLineForPo(
        DocumentLine source,
        PurchaseOrder po,
        CustomerOrder order,
        OrderProduct product)
    {
        var quantity = source.Quantity;
        var unitPrice = RmbMoneyService.Round(product.PurchaseUnitPrice);
        return new DocumentLine
        {
            DocumentType = "PO",
            DocumentId = po.Id,
            ProductId = source.ProductId,
            OrderProductId = product.Id,
            SourceDocumentLineId = source.Id,
            CustomerId = order.CustomerId,
            SupplierId = po.SupplierId,
            Sku = product.SystemSku,
            ProductName = product.NameCn,
            Unit = product.Unit,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Amount = RmbMoneyService.Round(quantity * unitPrice),
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
            PurchaseUnitPriceSnapshot = unitPrice,
            SalesUnitPriceSnapshot = RmbMoneyService.Round(product.SalesUnitPrice),
            SortNo = source.SortNo,
            Remark = source.Remark,
            CreatedAt = DateTime.Now
        };
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync()
        => _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;
}
