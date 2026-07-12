using System.Security.Claims;
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

    public record GeneratePoRequest(
        long SupplierId,
        DateTime? ExpectedDeliveryDate,
        string? Currency,
        string? DeliveryTerms,
        string? PaymentTerms);

    public record GeneratePosRequest(
        long SupplierId,
        List<GeneratePoItem> Items,
        DateTime? ExpectedDeliveryDate);

    public record WorkflowRequest(string? Reason);

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
            currency = RmbMoneyService.Currency,
            x.Status,
            x.DeliveryTerms,
            x.PaymentTerms,
            x.ImporterProfileId,
            x.LabelTemplateId,
            x.MarkTemplateId,
            x.ConfirmedAt,
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
        if (input.CustomerId <= 0)
            throw new BusinessRuleException("CUSTOMER_REQUIRED", "请选择客户");
        input.Id = 0;
        input.No = string.IsNullOrWhiteSpace(input.No) ? NumberService.NewNo("CO") : input.No.Trim();
        input.Currency = RmbMoneyService.Currency;
        input.Status = "draft";
        input.ConfirmedAt = null;
        input.ImporterSnapshotJson = "{}";
        input.LabelTemplateSnapshotJson = "{}";
        input.MarkTemplateSnapshotJson = "{}";
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
            Currency = RmbMoneyService.Currency,
            Status = "draft",
            DeliveryTerms = source.DeliveryTerms,
            PaymentTerms = source.PaymentTerms,
            ImporterProfileId = source.ImporterProfileId,
            LabelTemplateId = source.LabelTemplateId,
            MarkTemplateId = source.MarkTemplateId,
            ImporterSnapshotJson = "{}",
            LabelTemplateSnapshotJson = "{}",
            MarkTemplateSnapshotJson = "{}",
            Remark = $"复制自 {source.No}",
            CreatedAt = DateTime.Now
        };
        _db.CustomerOrders.Add(copy);
        await _db.SaveChangesAsync();

        var sourceProducts = await _db.OrderProducts
            .Where(x => x.SourceCustomerOrderId == source.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();
        if (sourceProducts.Count > 0)
        {
            var productService = new OrderProductService(_db);
            foreach (var product in sourceProducts)
                await productService.CopyToOrderAsync(product.Id, copy.Id, product.SupplierId);
        }
        else
        {
            await DocumentLineCopyService.CopyAsync(_db, "CO", source.Id, "CO", copy.Id);
            await _db.SaveChangesAsync();
        }
        return copy;
    }

    [HttpPost("{id:long}/confirm")]
    public async Task<ActionResult<CustomerOrder>> Confirm(long id, WorkflowRequest request)
    {
        var workflow = NewWorkflow();
        return await workflow.ConfirmAsync(id, request.Reason ?? "确认客户订单", CurrentUserId());
    }

    [HttpPost("{id:long}/reopen")]
    public async Task<ActionResult<CustomerOrder>> Reopen(long id, WorkflowRequest request)
    {
        var workflow = NewWorkflow();
        return await workflow.ReopenAsync(id, request.Reason ?? string.Empty, CurrentUserId());
    }

    [HttpPost("{id:long}/generate-pos")]
    public async Task<ActionResult<PurchaseOrder>> GeneratePos(long id, GeneratePosRequest request)
    {
        var workflow = NewWorkflow();
        return await workflow.GeneratePoAsync(
            id,
            request.Items,
            request.SupplierId,
            request.ExpectedDeliveryDate,
            CurrentUserId());
    }

    [HttpPost("{id:long}/generate-po")]
    public async Task<ActionResult<PurchaseOrder>> GeneratePo(long id, GeneratePoRequest request)
    {
        var source = await _db.CustomerOrders.FindAsync(id);
        if (source == null) return NotFound();
        if (request.SupplierId <= 0)
            throw new BusinessRuleException("SUPPLIER_REQUIRED", "请选择商品供应商");

        var activePoIds = await _db.PurchaseOrders
            .Where(x => x.CustomerOrderId == id && x.Status != "cancelled")
            .Select(x => x.Id)
            .ToListAsync();
        var purchasedProductIds = await _db.DocumentLines
            .Where(x => !x.IsDeleted && x.DocumentType == "PO" &&
                        activePoIds.Contains(x.DocumentId) && x.OrderProductId.HasValue)
            .Select(x => x.OrderProductId!.Value)
            .Distinct()
            .ToListAsync();
        var products = await _db.OrderProducts
            .Where(x => x.SourceCustomerOrderId == id && x.SupplierId == request.SupplierId &&
                        !purchasedProductIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();
        var lines = await _db.DocumentLines
            .Where(x => !x.IsDeleted && x.DocumentType == "CO" && x.DocumentId == id &&
                        x.OrderProductId.HasValue && products.Contains(x.OrderProductId.Value))
            .ToListAsync();
        if (lines.Count == 0)
            throw new BusinessRuleException("ORDER_PRODUCTS_REQUIRED", "没有可生成采购订单的完整订单商品");

        var workflow = NewWorkflow();
        var po = await workflow.GeneratePoAsync(
            id,
            lines.Select(x => new GeneratePoItem(x.OrderProductId!.Value, x.Quantity)).ToList(),
            request.SupplierId,
            request.ExpectedDeliveryDate,
            CurrentUserId());
        if (!string.IsNullOrWhiteSpace(request.DeliveryTerms)) po.DeliveryTerms = request.DeliveryTerms;
        if (!string.IsNullOrWhiteSpace(request.PaymentTerms)) po.PaymentTerms = request.PaymentTerms;
        po.Currency = RmbMoneyService.Currency;
        await _db.SaveChangesAsync();
        return po;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<CustomerOrder>> Update(long id, CustomerOrder input)
    {
        var entity = await _db.CustomerOrders.FindAsync(id);
        if (entity == null) return NotFound();
        if (entity.Status != "draft")
            throw new BusinessRuleException("ORDER_LOCKED", "客户订单已确认，必须先退回草稿");

        if (entity.CustomerId != input.CustomerId)
        {
            var hasProducts = await _db.OrderProducts.AnyAsync(x => x.SourceCustomerOrderId == entity.Id);
            if (hasProducts)
                throw new BusinessRuleException("ORDER_CUSTOMER_IMMUTABLE", "已有订单商品时不能更换客户");
        }

        entity.CustomerId = input.CustomerId;
        entity.BuyingTripId = input.BuyingTripId;
        entity.RfqId = input.RfqId;
        entity.OrderDate = input.OrderDate;
        entity.ExpectedDeliveryDate = input.ExpectedDeliveryDate;
        entity.Currency = RmbMoneyService.Currency;
        entity.DeliveryTerms = input.DeliveryTerms;
        entity.PaymentTerms = input.PaymentTerms;
        entity.ImporterProfileId = input.ImporterProfileId;
        entity.LabelTemplateId = input.LabelTemplateId;
        entity.MarkTemplateId = input.MarkTemplateId;
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
        if (entity.Status != "draft")
            throw new BusinessRuleException("ORDER_LOCKED", "只有草稿客户订单可以删除");
        var hasActivePo = await _db.PurchaseOrders
            .AnyAsync(x => x.CustomerOrderId == id && x.Status != "cancelled");
        if (hasActivePo)
            throw new BusinessRuleException("ORDER_HAS_DOWNSTREAM_DOCUMENTS", "客户订单已有采购订单，不能删除");

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        var products = await _db.OrderProducts.Where(x => x.SourceCustomerOrderId == id).ToListAsync();
        foreach (var product in products)
        {
            product.IsDeleted = true;
            product.UpdatedAt = DateTime.Now;
        }
        var lines = await _db.DocumentLines
            .Where(x => !x.IsDeleted && x.DocumentType == "CO" && x.DocumentId == id)
            .ToListAsync();
        foreach (var line in lines)
        {
            line.IsDeleted = true;
            line.UpdatedAt = DateTime.Now;
        }
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    private CustomerOrderWorkflowService NewWorkflow()
        => new(_db, new AuditTrailService(_db));

    private long? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(raw, out var id) ? id : null;
    }

    private static decimal CalculateTotalCbm(DocumentLine line)
    {
        var cartonCbm = line.CartonCbm;
        if (cartonCbm <= 0 && line.CartonLengthCm > 0 && line.CartonWidthCm > 0 && line.CartonHeightCm > 0)
            cartonCbm = line.CartonLengthCm * line.CartonWidthCm * line.CartonHeightCm / 1_000_000m;
        if (line.TotalCbm > 0) return line.TotalCbm;
        return cartonCbm * line.Cartons;
    }
}
