using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/purchase-orders")]
public class PurchaseOrderConfirmationController : ControllerBase
{
    private readonly AppDbContext _db;

    public PurchaseOrderConfirmationController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("{id:long}/confirm")]
    public async Task<ActionResult<PurchaseOrder>> Confirm(long id)
    {
        var purchaseOrder = await _db.PurchaseOrders.FindAsync(id);
        if (purchaseOrder == null) return NotFound();
        if (purchaseOrder.Status == "confirmed") return purchaseOrder;
        if (purchaseOrder.Status != "draft")
            throw new BusinessRuleException("PURCHASE_ORDER_NOT_DRAFT", "只有草稿采购订单可以确认");
        if (!purchaseOrder.CustomerId.HasValue || purchaseOrder.CustomerId.Value <= 0)
            throw new BusinessRuleException("CUSTOMER_REQUIRED", "采购订单必须选择客户");
        if (purchaseOrder.SupplierId <= 0)
            throw new BusinessRuleException("SUPPLIER_REQUIRED", "采购订单必须选择商品供应商");

        var lines = await _db.DocumentLines
            .Where(x => !x.IsDeleted && x.DocumentType == "PO" && x.DocumentId == id)
            .ToListAsync();
        if (lines.Count == 0)
            throw new BusinessRuleException("PURCHASE_ORDER_ITEMS_REQUIRED", "采购订单至少需要一个商品");
        if (lines.Any(x => !x.OrderProductId.HasValue || x.Cartons <= 0m || x.CartonQty <= 0m || x.Quantity <= 0m))
            throw new BusinessRuleException("PURCHASE_ORDER_PACKING_REQUIRED", "采购订单商品来源、数量和箱规必须完整");

        purchaseOrder.Currency = RmbMoneyService.Currency;
        purchaseOrder.Status = "confirmed";
        purchaseOrder.ConfirmedAt = DateTime.Now;
        purchaseOrder.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return purchaseOrder;
    }
}
