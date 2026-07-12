using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/label-mark-templates")]
public class LabelMarkTemplatesController : ControllerBase
{
    private static readonly string[] AllowedTypes = ["product_label", "carton_mark"];
    private static readonly string[] AllowedModes = ["fixed", "visual"];
    private readonly AppDbContext _db;

    public LabelMarkTemplatesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PrintTemplate>>> List(
        [FromQuery] long? customerId,
        [FromQuery] string? templateType,
        [FromQuery] string? status = "active")
    {
        var query = _db.PrintTemplates.Where(x => AllowedTypes.Contains(x.TemplateType));
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        if (!string.IsNullOrWhiteSpace(templateType)) query = query.Where(x => x.TemplateType == templateType);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return await query
            .OrderBy(x => x.TemplateType)
            .ThenByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<PrintTemplate>> Get(long id)
    {
        var entity = await _db.PrintTemplates.FindAsync(id);
        return entity is null || !AllowedTypes.Contains(entity.TemplateType) ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<PrintTemplate>> Create(PrintTemplate input)
    {
        Validate(input);
        input.Id = 0;
        Normalize(input);
        if (input.IsDefault) await ClearOtherDefaultsAsync(input.CustomerId!.Value, input.TemplateType, null);
        _db.PrintTemplates.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<PrintTemplate>> Update(long id, PrintTemplate input)
    {
        Validate(input);
        var entity = await _db.PrintTemplates.FindAsync(id);
        if (entity is null || !AllowedTypes.Contains(entity.TemplateType)) return NotFound();
        if (entity.CustomerId != input.CustomerId || entity.TemplateType != input.TemplateType)
            throw new BusinessRuleException(
                "PRINT_TEMPLATE_SCOPE_IMMUTABLE",
                "模板所属客户和模板类型不能修改");

        if (input.IsDefault) await ClearOtherDefaultsAsync(input.CustomerId!.Value, input.TemplateType, entity.Id);
        entity.Code = input.Code.Trim();
        entity.Name = input.Name.Trim();
        entity.DocumentType = "CO";
        entity.TemplateType = input.TemplateType;
        entity.ImporterProfileId = input.ImporterProfileId;
        entity.DesignerMode = input.DesignerMode;
        entity.Language = string.IsNullOrWhiteSpace(input.Language) ? "en" : input.Language.Trim();
        entity.PaperSize = string.IsNullOrWhiteSpace(input.PaperSize) ? "CUSTOM" : input.PaperSize.Trim();
        entity.PaperWidthMm = input.PaperWidthMm;
        entity.PaperHeightMm = input.PaperHeightMm;
        entity.Orientation = string.IsNullOrWhiteSpace(input.Orientation) ? "portrait" : input.Orientation.Trim();
        entity.LayoutJson = string.IsNullOrWhiteSpace(input.LayoutJson) ? "{}" : input.LayoutJson;
        entity.Body = input.Body;
        entity.IsDefault = input.IsDefault;
        entity.Status = string.IsNullOrWhiteSpace(input.Status) ? "active" : input.Status;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.PrintTemplates.FindAsync(id);
        if (entity is null || !AllowedTypes.Contains(entity.TemplateType)) return NotFound();
        var referenced = entity.TemplateType == "product_label"
            ? await _db.CustomerOrders.AnyAsync(x => x.LabelTemplateId == id) ||
              await _db.PurchaseOrders.AnyAsync(x => x.LabelTemplateId == id) ||
              await _db.OrderProducts.AnyAsync(x => x.LabelTemplateId == id)
            : await _db.CustomerOrders.AnyAsync(x => x.MarkTemplateId == id) ||
              await _db.PurchaseOrders.AnyAsync(x => x.MarkTemplateId == id) ||
              await _db.OrderProducts.AnyAsync(x => x.MarkTemplateId == id);
        if (referenced)
            throw new BusinessRuleException("PRINT_TEMPLATE_IN_USE", "模板已被业务单据使用，不能删除");

        var wasDefault = entity.IsDefault;
        entity.IsDeleted = true;
        entity.IsDefault = false;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        if (wasDefault && entity.CustomerId.HasValue)
        {
            var next = await _db.PrintTemplates
                .Where(x => x.CustomerId == entity.CustomerId.Value &&
                            x.TemplateType == entity.TemplateType && x.Status == "active")
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync();
            if (next is not null)
            {
                next.IsDefault = true;
                next.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
            }
        }
        return Ok(new { ok = true });
    }

    private static void Validate(PrintTemplate input)
    {
        if (!input.CustomerId.HasValue || input.CustomerId.Value <= 0)
            throw new BusinessRuleException("CUSTOMER_REQUIRED", "请选择客户");
        if (!AllowedTypes.Contains(input.TemplateType))
            throw new BusinessRuleException("PRINT_TEMPLATE_TYPE_INVALID", "模板类型无效");
        if (!AllowedModes.Contains(input.DesignerMode))
            throw new BusinessRuleException("PRINT_TEMPLATE_MODE_INVALID", "模板设计模式无效");
        if (string.IsNullOrWhiteSpace(input.Code))
            throw new BusinessRuleException("PRINT_TEMPLATE_CODE_REQUIRED", "模板编码不能为空");
        if (string.IsNullOrWhiteSpace(input.Name))
            throw new BusinessRuleException("PRINT_TEMPLATE_NAME_REQUIRED", "模板名称不能为空");
        if (input.PaperWidthMm is <= 0 || input.PaperHeightMm is <= 0)
            throw new BusinessRuleException("PRINT_TEMPLATE_SIZE_INVALID", "模板宽度和高度必须大于零");
        if (input.DesignerMode == "fixed" && string.IsNullOrWhiteSpace(input.Body))
            throw new BusinessRuleException("PRINT_TEMPLATE_BODY_REQUIRED", "固定模板内容不能为空");
        if (input.DesignerMode == "visual" && string.IsNullOrWhiteSpace(input.LayoutJson))
            throw new BusinessRuleException("PRINT_TEMPLATE_LAYOUT_REQUIRED", "可视化模板布局不能为空");
    }

    private static void Normalize(PrintTemplate input)
    {
        input.Code = input.Code.Trim();
        input.Name = input.Name.Trim();
        input.DocumentType = "CO";
        input.Language = string.IsNullOrWhiteSpace(input.Language) ? "en" : input.Language.Trim();
        input.PaperSize = string.IsNullOrWhiteSpace(input.PaperSize) ? "CUSTOM" : input.PaperSize.Trim();
        input.Orientation = string.IsNullOrWhiteSpace(input.Orientation) ? "portrait" : input.Orientation.Trim();
        input.LayoutJson = string.IsNullOrWhiteSpace(input.LayoutJson) ? "{}" : input.LayoutJson;
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "active" : input.Status;
        input.CreatedAt = DateTime.Now;
    }

    private async Task ClearOtherDefaultsAsync(long customerId, string templateType, long? exceptId)
    {
        var defaults = await _db.PrintTemplates
            .Where(x => x.CustomerId == customerId && x.TemplateType == templateType && x.IsDefault &&
                        (!exceptId.HasValue || x.Id != exceptId.Value))
            .ToListAsync();
        foreach (var item in defaults)
        {
            item.IsDefault = false;
            item.UpdatedAt = DateTime.Now;
        }
    }
}
