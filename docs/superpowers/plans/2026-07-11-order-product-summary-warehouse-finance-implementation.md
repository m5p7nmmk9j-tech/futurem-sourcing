# 订单商品、客户汇总、仓库装柜与财务联动 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 FUTUREM Sourcing 从固定商品/通用明细流程升级为订单商品快照、整箱汇总预留、送货收货验货、可追溯仓库库存、三天装柜锁定、一柜一出运单以及人民币统一应收应付的完整业务闭环。

**Architecture:** 保留现有 ASP.NET Core + EF Core + MySQL + Vue 3 架构，以新的领域实体和服务承载关键业务规则，控制器只负责请求校验和命令调度。所有确认动作通过事务、唯一来源键和幂等服务完成；旧 `DocumentLine`、`Currency`、`SummaryOrderId` 字段保留兼容读取，新写入走订单商品、库存批次和财务明细模型。

**Tech Stack:** .NET 9、ASP.NET Core Web API、Entity Framework Core 9、Pomelo MySQL、Vue 3、TypeScript、Element Plus、Vite、xUnit、EF Core InMemory、Vitest、Playwright、Docker Compose。

## Global Constraints

- 所有金额固定人民币；前端无币种选择，后端将所有兼容 `Currency` 字段强制写为 `RMB`。
- 一条 CO 订单商品全部未采购数量只能进入一张有效 PO，不能拆给多个商品供应商。
- 一张 PO 只能有一个商品供应商。
- 同一订单统一使用一个进口商、一个商品标签模板和一个外箱唛头模板。
- PO 明细可在客户汇总阶段按整箱拆分到多张汇总单，累计不得超过 PO 箱数。
- 一张送货通知只能对应一个供应商、一个汇总单、一个计划日期和一个仓库。
- 一张收货单只能有一张有效验货单。
- 供应商商品应付只按验货最终实际接受数量生成。
- 一张装柜单只能包含一个客户、一个仓库的库存。
- 装柜草稿锁定库存 72 小时；普通保存不续期；到期释放库存但保留草稿。
- 装柜确认只扣实际装走数量；未装走商品继续留仓，不创建剩余汇总单。
- 参与已确认装柜的原汇总单标记 `loaded`，不能再次用于装柜。
- 一张装柜单只能生成一张出运单。
- 装柜确认生成商品货款应收；出运确认把物流费用追加到同一客户应收。
- 商品供应商与物流服务商主数据、应付和预付款分别核算。
- 客户收款、供应商付款、预收和预付均按最早未结清明细先进先出自动冲销。
- 已确认业务和已发生收付款不得直接覆盖或删除；必须使用解锁、调整或反冲并写审计日志。
- 所有关键确认接口必须幂等并在单个数据库事务中完成。

---

## 文件结构与责任边界

### 新增后端实体

- `api/Futurem.Sourcing.Api/Entities/OrderProduct.cs`
- `api/Futurem.Sourcing.Api/Entities/OrderProductImage.cs`
- `api/Futurem.Sourcing.Api/Entities/CustomerImporterProfile.cs`
- `api/Futurem.Sourcing.Api/Entities/SummaryOrderItem.cs`
- `api/Futurem.Sourcing.Api/Entities/DeliveryNotice.cs`
- `api/Futurem.Sourcing.Api/Entities/DeliveryNoticeLine.cs`
- `api/Futurem.Sourcing.Api/Entities/QcOrderLine.cs`
- `api/Futurem.Sourcing.Api/Entities/Warehouse.cs`
- `api/Futurem.Sourcing.Api/Entities/WarehouseLocation.cs`
- `api/Futurem.Sourcing.Api/Entities/InventoryLot.cs`
- `api/Futurem.Sourcing.Api/Entities/InventoryTransaction.cs`
- `api/Futurem.Sourcing.Api/Entities/InventoryReservation.cs`
- `api/Futurem.Sourcing.Api/Entities/ContainerLoadSource.cs`
- `api/Futurem.Sourcing.Api/Entities/LogisticsProvider.cs`
- `api/Futurem.Sourcing.Api/Entities/FinanceRecordLine.cs`
- `api/Futurem.Sourcing.Api/Entities/PaymentAllocation.cs`
- `api/Futurem.Sourcing.Api/Entities/CustomerAdvance.cs`
- `api/Futurem.Sourcing.Api/Entities/CustomerAdvanceUsage.cs`
- `api/Futurem.Sourcing.Api/Entities/FinancialAdjustment.cs`
- `api/Futurem.Sourcing.Api/Entities/PrintPackagePublication.cs`
- `api/Futurem.Sourcing.Api/Entities/PrintPackageDownloadLog.cs`

### 新增后端服务

- `api/Futurem.Sourcing.Api/Services/BusinessRuleException.cs`
- `api/Futurem.Sourcing.Api/Services/RmbMoneyService.cs`
- `api/Futurem.Sourcing.Api/Services/AuditTrailService.cs`
- `api/Futurem.Sourcing.Api/Services/OrderProductService.cs`
- `api/Futurem.Sourcing.Api/Services/CustomerOrderWorkflowService.cs`
- `api/Futurem.Sourcing.Api/Services/SummaryReservationService.cs`
- `api/Futurem.Sourcing.Api/Services/DeliveryNoticeService.cs`
- `api/Futurem.Sourcing.Api/Services/QcConfirmationService.cs`
- `api/Futurem.Sourcing.Api/Services/InventoryService.cs`
- `api/Futurem.Sourcing.Api/Services/ContainerReservationService.cs`
- `api/Futurem.Sourcing.Api/Services/ContainerConfirmationService.cs`
- `api/Futurem.Sourcing.Api/Services/ShipmentDepartureService.cs`
- `api/Futurem.Sourcing.Api/Services/FinanceDocumentService.cs`
- `api/Futurem.Sourcing.Api/Services/FifoSettlementService.cs`
- `api/Futurem.Sourcing.Api/Services/LabelMarkPackageService.cs`
- `api/Futurem.Sourcing.Api/Services/ContainerReservationExpiryWorker.cs`

### 新增后端控制器

- `api/Futurem.Sourcing.Api/Controllers/OrderProductsController.cs`
- `api/Futurem.Sourcing.Api/Controllers/CustomerImporterProfilesController.cs`
- `api/Futurem.Sourcing.Api/Controllers/DeliveryNoticesController.cs`
- `api/Futurem.Sourcing.Api/Controllers/WarehousesController.cs`
- `api/Futurem.Sourcing.Api/Controllers/WarehouseLocationsController.cs`
- `api/Futurem.Sourcing.Api/Controllers/InventoryController.cs`
- `api/Futurem.Sourcing.Api/Controllers/LogisticsProvidersController.cs`
- `api/Futurem.Sourcing.Api/Controllers/FinancialAdjustmentsController.cs`
- `api/Futurem.Sourcing.Api/Controllers/SupplierPortalController.cs`

### 修改后端文件

- `api/Futurem.Sourcing.Api/Entities/CustomerOrder.cs`
- `api/Futurem.Sourcing.Api/Entities/PurchaseOrder.cs`
- `api/Futurem.Sourcing.Api/Entities/DocumentLine.cs`
- `api/Futurem.Sourcing.Api/Entities/SummaryOrder.cs`
- `api/Futurem.Sourcing.Api/Entities/ReceivingOrder.cs`
- `api/Futurem.Sourcing.Api/Entities/QcOrder.cs`
- `api/Futurem.Sourcing.Api/Entities/ContainerLoad.cs`
- `api/Futurem.Sourcing.Api/Entities/Shipment.cs`
- `api/Futurem.Sourcing.Api/Entities/ShipmentExpense.cs`
- `api/Futurem.Sourcing.Api/Entities/FinanceRecord.cs`
- `api/Futurem.Sourcing.Api/Entities/Payment.cs`
- `api/Futurem.Sourcing.Api/Entities/SupplierPrepayment.cs`
- `api/Futurem.Sourcing.Api/Entities/PrintTemplate.cs`
- `api/Futurem.Sourcing.Api/Entities/Supplier.cs`
- `api/Futurem.Sourcing.Api/Data/AppDbContext.cs`
- `api/Futurem.Sourcing.Api/Services/DatabaseUpgradeService.cs`
- `api/Futurem.Sourcing.Api/Controllers/CustomerOrdersController.cs`
- `api/Futurem.Sourcing.Api/Controllers/PurchaseOrdersController.cs`
- `api/Futurem.Sourcing.Api/Controllers/SummaryOrdersController.cs`
- `api/Futurem.Sourcing.Api/Controllers/ReceivingOrdersController.cs`
- `api/Futurem.Sourcing.Api/Controllers/QcOrdersController.cs`
- `api/Futurem.Sourcing.Api/Controllers/ContainerLoadsController.cs`
- `api/Futurem.Sourcing.Api/Controllers/ShipmentsController.cs`
- `api/Futurem.Sourcing.Api/Controllers/ShipmentExpensesController.cs`
- `api/Futurem.Sourcing.Api/Controllers/FinanceRecordsController.cs`
- `api/Futurem.Sourcing.Api/Controllers/PaymentsController.cs`
- `api/Futurem.Sourcing.Api/Controllers/PrintCenterController.cs`
- `api/Futurem.Sourcing.Api/Controllers/GlobalSearchController.cs`
- `api/Futurem.Sourcing.Api/Controllers/RbacController.cs`
- `api/Futurem.Sourcing.Api/Program.cs`

### 新增前端文件

- `web/src/types/orderProduct.ts`
- `web/src/types/inventory.ts`
- `web/src/types/finance.ts`
- `web/src/utils/rmb.ts`
- `web/src/utils/containerReservation.ts`
- `web/src/components/OrderProductEditor.vue`
- `web/src/components/CustomerImporterSelector.vue`
- `web/src/components/LabelMarkTemplateSelector.vue`
- `web/src/components/SummaryAllocationEditor.vue`
- `web/src/components/DeliveryNoticeLines.vue`
- `web/src/components/QcResultEditor.vue`
- `web/src/components/InventoryPicker.vue`
- `web/src/components/ContainerReservationStatus.vue`
- `web/src/components/LogisticsExpenseEditor.vue`
- `web/src/components/FinanceRecordLines.vue`
- `web/src/views/CustomerHistoryProducts.vue`
- `web/src/views/CustomerImporterProfiles.vue`
- `web/src/views/LabelMarkTemplates.vue`
- `web/src/views/DeliveryNotices.vue`
- `web/src/views/Warehouses.vue`
- `web/src/views/Inventory.vue`
- `web/src/views/LogisticsProviders.vue`
- `web/src/views/FinancialAdjustments.vue`
- `web/src/views/SupplierPortal.vue`

### 修改前端文件

- `web/src/views/CustomerOrders.vue`
- `web/src/views/PurchaseOrders.vue`
- `web/src/views/SummaryOrders.vue`
- `web/src/views/ReceivingOrders.vue`
- `web/src/views/QcOrders.vue`
- `web/src/views/ContainerLoads.vue`
- `web/src/views/Shipments.vue`
- `web/src/views/FinanceRecords.vue`
- `web/src/views/Suppliers.vue`
- `web/src/views/PrintCenter.vue`
- `web/src/components/DocumentLinesEditor.vue`
- `web/src/layouts/MainLayout.vue`
- `web/src/router.ts`
- `web/package.json`
- `scripts/check.sh`

---

### Task 1: 建立统一业务错误、人民币和审计基础

**Files:**
- Create: `api/Futurem.Sourcing.Api/Services/BusinessRuleException.cs`
- Create: `api/Futurem.Sourcing.Api/Services/RmbMoneyService.cs`
- Create: `api/Futurem.Sourcing.Api/Services/AuditTrailService.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/RmbMoneyServiceTests.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/AuditTrailServiceTests.cs`
- Modify: `api/Futurem.Sourcing.Api/Program.cs`

**Interfaces:**
- Produces: `BusinessRuleException.Code`, `BusinessRuleException.Details`。
- Produces: `RmbMoneyService.Round(decimal)`, `RmbMoneyService.NormalizeCurrency(string?)`。
- Produces: `AuditTrailService.WriteAsync(string entityType, long entityId, string action, object? before, object? after, string? reason, long? userId, string? correlationId = null)`。

- [ ] **Step 1: 写人民币失败测试**

```csharp
using Futurem.Sourcing.Api.Services;

namespace Futurem.Sourcing.Api.Tests.Services;

public class RmbMoneyServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("USD")]
    [InlineData("RMB")]
    public void NormalizeCurrency_AlwaysReturnsRmb(string? input)
        => Assert.Equal("RMB", RmbMoneyService.NormalizeCurrency(input));

    [Fact]
    public void Round_UsesTwoDecimalsAwayFromZero()
        => Assert.Equal(12.35m, RmbMoneyService.Round(12.345m));
}
```

- [ ] **Step 2: 运行测试确认失败**

Run:

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter RmbMoneyServiceTests
```

Expected: FAIL，提示 `RmbMoneyService` 不存在。

- [ ] **Step 3: 实现业务错误和人民币服务**

```csharp
namespace Futurem.Sourcing.Api.Services;

public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string code, string message, object? details = null) : base(message)
    {
        Code = code;
        Details = details;
    }

    public string Code { get; }
    public object? Details { get; }
}

public static class RmbMoneyService
{
    public const string Currency = "RMB";
    public static string NormalizeCurrency(string? _) => Currency;
    public static decimal Round(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
```

- [ ] **Step 4: 实现审计服务并写测试**

```csharp
using System.Text.Json;
using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;

namespace Futurem.Sourcing.Api.Services;

public sealed class AuditTrailService
{
    private readonly AppDbContext _db;
    public AuditTrailService(AppDbContext db) => _db = db;

    public async Task WriteAsync(
        string entityType,
        long entityId,
        string action,
        object? before,
        object? after,
        string? reason,
        long? userId,
        string? correlationId = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after),
            Reason = reason,
            UserId = userId,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();
    }
}
```

测试必须保存一条日志并断言 `BeforeJson`、`AfterJson`、`Reason` 非空。

- [ ] **Step 5: 注册服务和统一异常响应**

在 `Program.cs` 注册：

```csharp
builder.Services.AddScoped<AuditTrailService>();
```

在控制器异常过滤器或中间件中把 `BusinessRuleException` 转为：

```json
{ "code": "ORDER_LOCKED", "message": "订单已确认", "details": null }
```

- [ ] **Step 6: 运行测试和构建**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter "RmbMoneyServiceTests|AuditTrailServiceTests"
dotnet build api/Futurem.Sourcing.Api/Futurem.Sourcing.Api.csproj -c Release
```

Expected: PASS。

- [ ] **Step 7: 提交**

```bash
git add api/Futurem.Sourcing.Api api/Futurem.Sourcing.Api.Tests
git commit -m "feat: add business rule and audit foundations"
```

---

### Task 2: 新增订单商品、图片、进口商和模板数据模型

**Files:**
- Create: `api/Futurem.Sourcing.Api/Entities/OrderProduct.cs`
- Create: `api/Futurem.Sourcing.Api/Entities/OrderProductImage.cs`
- Create: `api/Futurem.Sourcing.Api/Entities/CustomerImporterProfile.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/PrintTemplate.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/CustomerOrder.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/PurchaseOrder.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/DocumentLine.cs`
- Modify: `api/Futurem.Sourcing.Api/Data/AppDbContext.cs`
- Modify: `api/Futurem.Sourcing.Api/Services/DatabaseUpgradeService.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/OrderProductSchemaTests.cs`

**Interfaces:**
- Produces DbSets: `OrderProducts`, `OrderProductImages`, `CustomerImporterProfiles`。
- Produces unique indexes: `CustomerId + CustomerBarcode`、有效 `OrderProductId + ImageType(main)`。
- CustomerOrder produces: `ImporterProfileId`, `LabelTemplateId`, `MarkTemplateId`, three snapshot JSON fields, `ConfirmedAt`。

- [ ] **Step 1: 写模型元数据失败测试**

```csharp
using Futurem.Sourcing.Api.Entities;

namespace Futurem.Sourcing.Api.Tests.Services;

public class OrderProductSchemaTests
{
    [Fact]
    public void OrderProduct_HasRequiredSnapshotFields()
    {
        var names = typeof(OrderProduct).GetProperties().Select(x => x.Name).ToHashSet();
        Assert.Contains("CustomerBarcode", names);
        Assert.Contains("PurchaseUnitPrice", names);
        Assert.Contains("SalesUnitPrice", names);
        Assert.Contains("ImporterSnapshotJson", names);
        Assert.Contains("LabelTemplateSnapshotJson", names);
        Assert.Contains("MarkTemplateSnapshotJson", names);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter OrderProductSchemaTests
```

Expected: FAIL。

- [ ] **Step 3: 创建 `OrderProduct`**

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class OrderProduct : BaseEntity
{
    public long CustomerId { get; set; }
    public long SupplierId { get; set; }
    public long? SourceOrderProductId { get; set; }
    public long SourceCustomerOrderId { get; set; }
    [MaxLength(80)] public string SystemSku { get; set; } = string.Empty;
    [MaxLength(120)] public string? CustomerItemNo { get; set; }
    [MaxLength(120)] public string CustomerBarcode { get; set; } = string.Empty;
    [MaxLength(120)] public string? SupplierItemNo { get; set; }
    public string NameCn { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? NameEs { get; set; }
    public string? Specification { get; set; }
    public string? Color { get; set; }
    public string Unit { get; set; } = "PCS";
    [Column(TypeName = "decimal(18,2)")] public decimal PurchaseUnitPrice { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal SalesUnitPrice { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CartonQty { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CartonLengthCm { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CartonWidthCm { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CartonHeightCm { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CartonCbm { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CartonGwKg { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CartonNwKg { get; set; }
    public long ImporterProfileId { get; set; }
    public string ImporterSnapshotJson { get; set; } = "{}";
    public long LabelTemplateId { get; set; }
    public string LabelTemplateSnapshotJson { get; set; } = "{}";
    public long MarkTemplateId { get; set; }
    public string MarkTemplateSnapshotJson { get; set; } = "{}";
    [MaxLength(20)] public string BatchCode { get; set; } = string.Empty;
    public string Status { get; set; } = "draft";
    public DateTime? LockedAt { get; set; }
}
```

- [ ] **Step 4: 创建图片和进口商实体**

`OrderProductImage` 保存 `OrderProductId`、`ImageUrl`、`ImageType`、`SortNo`、`FileName`、`ContentType`。  
`CustomerImporterProfile` 保存客户、公司、RFC、地址、Logo、默认原产地文字、默认模板和 `IsDefault`。

完整实体必须继承 `BaseEntity`，所有字符串初始化为空字符串或可空，不使用未初始化非空属性。

- [ ] **Step 5: 扩展现有实体和 DbContext**

`DocumentLine` 增加：

```csharp
public long? OrderProductId { get; set; }
public long? SourceDocumentLineId { get; set; }
public long? CustomerId { get; set; }
public long? SupplierId { get; set; }
[Column(TypeName = "decimal(18,2)")] public decimal PurchaseUnitPriceSnapshot { get; set; }
[Column(TypeName = "decimal(18,2)")] public decimal SalesUnitPriceSnapshot { get; set; }
public long? WarehouseId { get; set; }
public long? WarehouseLocationId { get; set; }
public long? InventoryLotId { get; set; }
```

`AppDbContext` 增加 DbSet、表名、唯一索引和客户默认进口商唯一约束。

- [ ] **Step 6: 增加可重复升级脚本**

`DatabaseUpgradeService.TargetVersion` 更新为 `2.0.0`，新增 `EnsureOrderProductSchemaAsync()` 并在 `UpgradeAsync()` 中调用。方法创建三张新表、扩展旧表，并把所有兼容币种更新为 `RMB`。

- [ ] **Step 7: 运行测试和重复升级测试**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter OrderProductSchemaTests
```

Expected: PASS；第二次执行升级不抛重复列或索引错误。

- [ ] **Step 8: 提交**

```bash
git add api/Futurem.Sourcing.Api/Entities api/Futurem.Sourcing.Api/Data api/Futurem.Sourcing.Api/Services/DatabaseUpgradeService.cs api/Futurem.Sourcing.Api.Tests
git commit -m "feat: add order product and importer schema"
```

---

### Task 3: 实现订单商品历史复制、整单模板和 CO 锁定

**Files:**
- Create: `api/Futurem.Sourcing.Api/Services/OrderProductService.cs`
- Create: `api/Futurem.Sourcing.Api/Services/CustomerOrderWorkflowService.cs`
- Create: `api/Futurem.Sourcing.Api/Controllers/OrderProductsController.cs`
- Create: `api/Futurem.Sourcing.Api/Controllers/CustomerImporterProfilesController.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/CustomerOrdersController.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/PurchaseOrdersController.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/CustomerOrderWorkflowServiceTests.cs`
- Modify: `api/Futurem.Sourcing.Api/Program.cs`

**Interfaces:**
- `OrderProductService.CopyToOrderAsync(long sourceOrderProductId, long targetCustomerOrderId, long supplierId)`。
- `CustomerOrderWorkflowService.ConfirmAsync(long orderId, string reason, long? userId)`。
- `CustomerOrderWorkflowService.ReopenAsync(long orderId, string reason, long? userId)`。
- `CustomerOrderWorkflowService.GeneratePoAsync(long orderId, IReadOnlyCollection<long> orderProductIds, long supplierId, DateTime? expectedDeliveryDate, long? userId)`。

- [ ] **Step 1: 写整单模板和整商品进入 PO 的失败测试**

```csharp
[Fact]
public async Task GeneratePo_RejectsPartialOrderProductQuantity()
{
    using var db = TestDbFactory.Create();
    var service = new CustomerOrderWorkflowService(db, new AuditTrailService(db));
    // 建立数量 100 的订单商品后，请求只生成 60。
    var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
        service.GeneratePoAsync(orderId, new[] { new GeneratePoItem(orderProductId, 60m) }, supplierId, null, null));
    Assert.Equal("ORDER_PRODUCT_MUST_CONVERT_FULL_QUANTITY", ex.Code);
}
```

另写测试确认同一 CO 所有订单商品的进口商、标签模板和唛头模板 ID 相同。

- [ ] **Step 2: 运行测试确认失败**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter CustomerOrderWorkflowServiceTests
```

Expected: FAIL。

- [ ] **Step 3: 实现历史复制**

复制服务必须：

1. 创建新 `OrderProduct`。
2. 设置 `SourceOrderProductId`。
3. 替换目标 CO、客户和可选供应商。
4. 复制所有图片为新行。
5. 保持来源记录不变。
6. 生成新批次草稿值 `DateTime.Today.ToString("yyyyMMdd")`。

- [ ] **Step 4: 实现 CO 确认和退回草稿**

确认前验证：客户、进口商、两个模板、每条商品供应商、客户条码、采购价、销售价、箱规、数量和箱数完整。确认时把订单头模板快照同步复制到所有订单商品并设置 `LockedAt`。

退回草稿要求 `reason` 非空并写审计日志；已产生收货、验货或财务时拒绝退回。

- [ ] **Step 5: 实现生成 PO**

服务必须使用事务并执行：

```csharp
if (requestedQuantity != remainingQuantity)
    throw new BusinessRuleException(
        "ORDER_PRODUCT_MUST_CONVERT_FULL_QUANTITY",
        "订单商品必须以全部未采购数量进入一张采购订单");
```

同一订单商品若已有非取消 PO 行，返回 `ORDER_PRODUCT_ALREADY_PURCHASED`。生成 PO 后复制 `OrderProductId` 和采购/销售价格快照，不修改订单商品供应商。

- [ ] **Step 6: 增加命令接口**

- `POST /api/customer-orders/{id}/confirm`
- `POST /api/customer-orders/{id}/reopen`
- `POST /api/customer-orders/{id}/generate-pos`
- `GET /api/order-products/history?customerId=&supplierId=&keyword=`
- `POST /api/order-products/{id}/copy-to-order`

旧 `/generate-po` 暂时保留但内部调用新服务且只接受整条订单商品。

- [ ] **Step 7: 运行测试**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter CustomerOrderWorkflowServiceTests
```

Expected: PASS。

- [ ] **Step 8: 提交**

```bash
git add api/Futurem.Sourcing.Api api/Futurem.Sourcing.Api.Tests
git commit -m "feat: add order product workflow and locking"
```

---

### Task 4: 改造订单商品、进口商和模板前端

**Files:**
- Create: `web/src/types/orderProduct.ts`
- Create: `web/src/utils/rmb.ts`
- Create: `web/src/components/OrderProductEditor.vue`
- Create: `web/src/components/CustomerImporterSelector.vue`
- Create: `web/src/components/LabelMarkTemplateSelector.vue`
- Create: `web/src/views/CustomerHistoryProducts.vue`
- Create: `web/src/views/CustomerImporterProfiles.vue`
- Create: `web/src/views/LabelMarkTemplates.vue`
- Modify: `web/src/views/CustomerOrders.vue`
- Modify: `web/src/components/DocumentLinesEditor.vue`
- Modify: `web/src/router.ts`
- Modify: `web/src/layouts/MainLayout.vue`
- Create: `web/tests/rmb.test.ts`
- Create: `web/tests/orderProduct.test.ts`
- Modify: `web/package.json`

**Interfaces:**
- `formatRmb(value: number): string`。
- `validateOrderProductDraft(product): string[]`。
- `OrderProductEditor` emits `saved` and receives `customerOrderId`, `locked`。

- [ ] **Step 1: 添加 Vitest 并写失败测试**

`package.json` 增加：

```json
{
  "scripts": {
    "test": "vitest run"
  },
  "devDependencies": {
    "vitest": "^2.1.8",
    "@vue/test-utils": "^2.4.6",
    "jsdom": "^25.0.1"
  }
}
```

测试：

```ts
import { describe, expect, it } from 'vitest'
import { formatRmb } from '../src/utils/rmb'

describe('formatRmb', () => {
  it('formats with two decimals', () => expect(formatRmb(1234.5)).toBe('¥1,234.50'))
})
```

- [ ] **Step 2: 运行测试确认失败**

```bash
cd web && npm test -- rmb.test.ts
```

Expected: FAIL，模块不存在。

- [ ] **Step 3: 实现人民币工具和订单商品类型**

```ts
export function formatRmb(value: number | string | null | undefined): string {
  const n = Number(value || 0)
  return `¥${n.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}
```

类型必须包含订单商品、图片、模板和锁定状态的全部字段。

- [ ] **Step 4: 替换 CO 明细编辑器**

`CustomerOrders.vue` 使用 `OrderProductEditor`，头部统一选择客户、进口商、标签模板、唛头模板；移除币种输入。确认后所有输入只读，只显示“退回草稿”。

- [ ] **Step 5: 建立客户历史商品页面**

页面支持客户、供应商、货号、条码、名称筛选；“复制到当前 CO”调用 `copy-to-order`，不直接复用历史 ID。

- [ ] **Step 6: 更新菜单和路由**

- `/products` 重定向 `/customer-history-products`
- 删除 `/markets` 入口
- 新增 `/customer-importers`、`/label-mark-templates`
- 菜单名称使用中文业务名

- [ ] **Step 7: 运行前端测试和构建**

```bash
cd web
npm test
npm run build
```

Expected: PASS。

- [ ] **Step 8: 提交**

```bash
git add web
git commit -m "feat: add order product and template workspace"
```

---

### Task 5: 实现客户汇总整箱预留

**Files:**
- Create: `api/Futurem.Sourcing.Api/Entities/SummaryOrderItem.cs`
- Create: `api/Futurem.Sourcing.Api/Services/SummaryReservationService.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/SummaryOrder.cs`
- Modify: `api/Futurem.Sourcing.Api/Data/AppDbContext.cs`
- Modify: `api/Futurem.Sourcing.Api/Services/DatabaseUpgradeService.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/SummaryOrdersController.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/SummaryReservationServiceTests.cs`
- Create: `web/src/components/SummaryAllocationEditor.vue`
- Modify: `web/src/views/SummaryOrders.vue`

**Interfaces:**
- `ReserveAsync(long summaryOrderId, long purchaseOrderLineId, decimal cartons, long? userId)`。
- `ReleaseAsync(long summaryOrderItemId, string reason, long? userId)`。
- `ConfirmAsync(long summaryOrderId, long? userId)`。
- `AppendAsync(long summaryOrderId, IReadOnlyCollection<SummaryAppendItem> items, string reason, long? userId)`。

- [ ] **Step 1: 写并发和整箱失败测试**

测试必须覆盖：

- 箱数不是整数时 `SUMMARY_WHOLE_CARTONS_REQUIRED`。
- 两次预留累计超过 PO 箱数时 `SUMMARY_RESERVATION_CONFLICT`。
- 删除草稿项释放数量。
- 同一客户允许两张草稿汇总单。

- [ ] **Step 2: 运行测试确认失败**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter SummaryReservationServiceTests
```

Expected: FAIL。

- [ ] **Step 3: 创建实体和索引**

`SummaryOrderItem` 必须保存 PO、PO 行、订单商品、供应商、预留箱数/件数、状态和释放原因。索引覆盖：

```csharp
modelBuilder.Entity<SummaryOrderItem>()
    .HasIndex(x => new { x.PurchaseOrderLineId, x.ReservationStatus });
```

- [ ] **Step 4: 实现事务预留**

关系型数据库使用事务；查询同 PO 行所有 `draft_reserved`、`confirmed` 的箱数，执行：

```csharp
if (reservedCartons + requestCartons > poLine.Cartons)
    throw new BusinessRuleException("SUMMARY_RESERVATION_CONFLICT", "可汇总箱数不足");
```

写入后重新计算汇总单体积、重量、采购金额、销售金额和预计毛利。

- [ ] **Step 5: 改造汇总命令接口**

删除旧 `generate-from-pos` 的整单复制行为，新接口按 PO 行和整箱数量操作。旧接口返回 410 或内部转换为新预留请求，不再把 PO 状态直接改为 `summarized`。

- [ ] **Step 6: 实现前端分配器**

显示 PO 总箱数、草稿预留、确认汇总、剩余可用；输入只允许整数箱，实时计算件数、CBM、重量和金额。

- [ ] **Step 7: 运行测试和构建**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter SummaryReservationServiceTests
cd web && npm test && npm run build
```

- [ ] **Step 8: 提交**

```bash
git add api web
git commit -m "feat: add whole-carton summary reservations"
```

---

### Task 6: 实现供应商送货通知和分批收货

**Files:**
- Create: `api/Futurem.Sourcing.Api/Entities/DeliveryNotice.cs`
- Create: `api/Futurem.Sourcing.Api/Entities/DeliveryNoticeLine.cs`
- Create: `api/Futurem.Sourcing.Api/Services/DeliveryNoticeService.cs`
- Create: `api/Futurem.Sourcing.Api/Controllers/DeliveryNoticesController.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/ReceivingOrder.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/ReceivingOrdersController.cs`
- Modify: `api/Futurem.Sourcing.Api/Data/AppDbContext.cs`
- Modify: `api/Futurem.Sourcing.Api/Services/DatabaseUpgradeService.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/DeliveryNoticeServiceTests.cs`
- Create: `web/src/components/DeliveryNoticeLines.vue`
- Create: `web/src/views/DeliveryNotices.vue`

**Interfaces:**
- `GenerateForConfirmedSummaryAsync(long summaryOrderId, DateTime plannedDate, long warehouseId, long? userId)`。
- `PublishAsync(long deliveryNoticeId, long? userId)`。
- `CreateReceivingAsync(long deliveryNoticeId, IReadOnlyCollection<ReceivingLineInput> lines, long? userId)`。

- [ ] **Step 1: 写分组和超量失败测试**

确认：同供应商、同汇总、同日期、同仓库合并；任一条件不同生成不同通知。累计通知超过汇总确认数量返回 `DELIVERY_NOTICE_OVER_PLANNED`。

- [ ] **Step 2: 运行测试确认失败**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter DeliveryNoticeServiceTests
```

- [ ] **Step 3: 创建通知实体和唯一来源键**

通知行保存 `SummaryOrderItemId`、PO 行和订单商品；通知头保存供应商、仓库、计划日期、发布和供应商确认时间。

- [ ] **Step 4: 实现自动分组生成**

使用 `GroupBy(x => new { x.SupplierId, PlannedDate = plannedDate.Date, warehouseId })`，每组创建一张通知。生成过程必须幂等，业务来源键为 `summary:{summaryId}:supplier:{supplierId}:warehouse:{warehouseId}:date:{yyyyMMdd}`。

- [ ] **Step 5: 扩展收货单**

收货单改为引用 `DeliveryNoticeId` 和 `WarehouseId`；一张通知可创建多张收货单。创建收货时校验累计实际到货不超过通知数量，保存临时点数但不生成应付。

- [ ] **Step 6: 实现页面**

通知页面支持发布、供应商确认、创建收货；显示计划、已收、剩余、标签/唛头资料状态。

- [ ] **Step 7: 测试和提交**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter DeliveryNoticeServiceTests
cd web && npm test && npm run build
git add api web
git commit -m "feat: add delivery notices and partial receiving"
```

---

### Task 7: 实现一收货一验货、实际接受数量和供应商应付

**Files:**
- Create: `api/Futurem.Sourcing.Api/Entities/QcOrderLine.cs`
- Create: `api/Futurem.Sourcing.Api/Services/QcConfirmationService.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/QcOrder.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/FinanceRecord.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/QcOrdersController.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/FinanceRecordsController.cs`
- Modify: `api/Futurem.Sourcing.Api/Data/AppDbContext.cs`
- Modify: `api/Futurem.Sourcing.Api/Services/DatabaseUpgradeService.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/QcConfirmationServiceTests.cs`
- Create: `web/src/components/QcResultEditor.vue`
- Modify: `web/src/views/QcOrders.vue`

**Interfaces:**
- `ConfirmAsync(long qcOrderId, IReadOnlyCollection<QcLineResult> lines, long? userId)`。
- `UnlockAsync(long qcOrderId, string reason, long? userId)`。
- Produces supplier payable `SourceKey = qc:{qcOrderId}:line:{qcLineId}`。

- [ ] **Step 1: 写数量恒等式和唯一验货测试**

覆盖：

```text
到货 = 合格 + 不合格 + 退回 + 待处理
最终接受 <= 到货
```

同一 `ReceivingOrderId` 创建第二张有效 QC 返回 `RECEIVING_ALREADY_HAS_QC`。

- [ ] **Step 2: 写应付触发测试**

到货 100、最终接受 82、PO 采购单价 10，应付必须是 820；不合格、退回和待处理不计应付。

- [ ] **Step 3: 实现确认事务**

确认服务在一个事务内：校验行、写最终接受、调用库存入库接口、释放汇总差额、幂等生成应付、更新单据状态、写审计。

- [ ] **Step 4: 实现解锁和调整**

解锁原因必填。重新确认时：

- 未付款应付直接更新金额。
- 已付款且金额减少，创建 `FinancialAdjustment` 类型 `supplier_refund_or_credit`。
- 金额增加，创建补充应付行。
- 原付款和分配记录不修改。

- [ ] **Step 5: 实现前端验货编辑器**

每行显示到货、合格、不合格、退回、待处理、最终接受；前端只做即时提示，后端为最终校验。

- [ ] **Step 6: 测试和提交**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter QcConfirmationServiceTests
cd web && npm test && npm run build
git add api web
git commit -m "feat: confirm qc into accepted quantity payables"
```

---

### Task 8: 新增仓库、库位、库存批次和库存流水

**Files:**
- Create: `api/Futurem.Sourcing.Api/Entities/Warehouse.cs`
- Create: `api/Futurem.Sourcing.Api/Entities/WarehouseLocation.cs`
- Create: `api/Futurem.Sourcing.Api/Entities/InventoryLot.cs`
- Create: `api/Futurem.Sourcing.Api/Entities/InventoryTransaction.cs`
- Create: `api/Futurem.Sourcing.Api/Services/InventoryService.cs`
- Create: `api/Futurem.Sourcing.Api/Controllers/WarehousesController.cs`
- Create: `api/Futurem.Sourcing.Api/Controllers/WarehouseLocationsController.cs`
- Create: `api/Futurem.Sourcing.Api/Controllers/InventoryController.cs`
- Modify: `api/Futurem.Sourcing.Api/Data/AppDbContext.cs`
- Modify: `api/Futurem.Sourcing.Api/Services/DatabaseUpgradeService.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/InventoryServiceTests.cs`
- Create: `web/src/types/inventory.ts`
- Create: `web/src/views/Warehouses.vue`
- Create: `web/src/views/Inventory.vue`

**Interfaces:**
- `ReceiveAcceptedAsync(QcOrderLine line, long warehouseId, long? locationId, long? userId)`。
- `GetAvailableAsync(long inventoryLotId)`。
- `AdjustAsync(long inventoryLotId, decimal quantityDelta, decimal cartonsDelta, string reason, long? userId)`。

- [ ] **Step 1: 写库存来源链和余额测试**

确认库存批次保留客户、订单商品、PO、送货、收货、QC、汇总、供应商和批次。断言：

```text
AvailableQuantity = OnHandQuantity - LockedQuantity
AvailableCartons = OnHandCartons - LockedCartons
```

- [ ] **Step 2: 实现实体和索引**

唯一批次键使用 `QcOrderLineId + WarehouseId + WarehouseLocationId`，避免重复验货确认生成重复批次。

- [ ] **Step 3: 实现库存服务**

所有入库、调整、锁定、释放和出库必须同时写 `InventoryTransaction`，流水类型固定枚举字符串。库存值不得为负。

- [ ] **Step 4: 实现仓库和库存 API**

`GET /api/inventory` 支持客户、仓库、货号、条码、PO、供应商、收货单、原汇总单、批次和库位筛选。

- [ ] **Step 5: 实现页面**

库存页面列出在库、锁定、可用、箱数、CBM、重量和完整来源；点击数量查看库存流水。

- [ ] **Step 6: 测试和提交**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter InventoryServiceTests
cd web && npm test && npm run build
git add api web
git commit -m "feat: add traceable warehouse inventory"
```

---

### Task 9: 实现装柜草稿库存锁定和 72 小时自动释放

**Files:**
- Create: `api/Futurem.Sourcing.Api/Entities/InventoryReservation.cs`
- Create: `api/Futurem.Sourcing.Api/Services/ContainerReservationService.cs`
- Create: `api/Futurem.Sourcing.Api/Services/ContainerReservationExpiryWorker.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/ContainerLoad.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/ContainerLoadsController.cs`
- Modify: `api/Futurem.Sourcing.Api/Data/AppDbContext.cs`
- Modify: `api/Futurem.Sourcing.Api/Services/DatabaseUpgradeService.cs`
- Modify: `api/Futurem.Sourcing.Api/Program.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/ContainerReservationServiceTests.cs`
- Create: `web/src/utils/containerReservation.ts`
- Create: `web/src/components/InventoryPicker.vue`
- Create: `web/src/components/ContainerReservationStatus.vue`
- Modify: `web/src/views/ContainerLoads.vue`

**Interfaces:**
- `LockAsync(long containerLoadId, IReadOnlyCollection<InventoryReservationInput> items, long? userId)`。
- `ReleaseAsync(long containerLoadId, string reason, long? userId)`。
- `RelockAsync(long containerLoadId, IReadOnlyCollection<InventoryReservationInput> items, long? userId)`。
- `ExpireAsync(DateTime now)`。

- [ ] **Step 1: 写客户、仓库和超量失败测试**

覆盖 `CONTAINER_CUSTOMER_MISMATCH`、`CONTAINER_WAREHOUSE_MISMATCH`、`INVENTORY_NOT_AVAILABLE`。

- [ ] **Step 2: 写 72 小时测试**

锁定时间 `2026-07-11 09:00`，到 `2026-07-14 08:59` 仍有效，`09:00` 过期；普通保存不得修改 `ExpiresAt`。

- [ ] **Step 3: 实现锁定事务**

对每个库存批次检查可用数量，创建 reservation，增加 `LockedQuantity/LockedCartons`，写 `load_lock` 流水。`ExpiresAt = LockedAt.AddHours(72)`。

- [ ] **Step 4: 实现过期后台服务**

`BackgroundService` 每 15 分钟执行一次 `ExpireAsync(DateTime.Now)`；只释放 `active` reservation，把装柜单状态改为 `lock_expired`，不删除草稿。

- [ ] **Step 5: 实现前端库存选择和倒计时**

库存选择器固定客户和仓库；倒计时只显示，不通过保存续期。过期后按钮变为“重新锁定库存”。

- [ ] **Step 6: 测试和提交**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter ContainerReservationServiceTests
cd web && npm test && npm run build
git add api web
git commit -m "feat: add expiring container inventory reservations"
```

---

### Task 10: 实现装柜确认、商品应收和自动出运单

**Files:**
- Create: `api/Futurem.Sourcing.Api/Entities/ContainerLoadSource.cs`
- Create: `api/Futurem.Sourcing.Api/Services/ContainerConfirmationService.cs`
- Create: `api/Futurem.Sourcing.Api/Services/FinanceDocumentService.cs`
- Create: `api/Futurem.Sourcing.Api/Entities/FinanceRecordLine.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/ContainerLoad.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/Shipment.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/FinanceRecord.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/ContainerLoadsController.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/ShipmentsController.cs`
- Modify: `api/Futurem.Sourcing.Api/Data/AppDbContext.cs`
- Modify: `api/Futurem.Sourcing.Api/Services/DatabaseUpgradeService.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/ContainerConfirmationServiceTests.cs`

**Interfaces:**
- `ConfirmAsync(long containerLoadId, IReadOnlyCollection<ActualLoadInput> actualLines, long? userId)`。
- `FinanceDocumentService.EnsureCustomerReceivableForContainerAsync(long containerLoadId)`。
- Unique keys: `finance SourceKey = container:{id}:goods`、`shipment ContainerLoadId` unique。

- [ ] **Step 1: 写实际装柜和幂等测试**

库存 100、锁定 100、实际装 70：确认后在库 30、锁定 0、商品应收按 70 计算、重复确认不再次扣库存或生成第二张应收/出运单。

- [ ] **Step 2: 写剩余库存和汇总状态测试**

断言未装 30 仍在原库存批次中；涉及的原汇总单状态为 `loaded`；第二次装柜可直接选择这 30，不要求新汇总单。

- [ ] **Step 3: 实现确认事务**

依次执行：验证锁定、保存实际数量、扣库存、关闭 reservation、写 `container_out` 流水、创建商品应收头和行、创建出运草稿、更新原汇总单、写审计。

- [ ] **Step 4: 创建一柜一出运唯一约束**

```csharp
modelBuilder.Entity<Shipment>()
    .HasIndex(x => x.ContainerLoadId)
    .IsUnique();
```

旧 `generate-from-container` 改为调用幂等 `EnsureShipmentAsync`；装柜确认自动调用，不再要求手工生成。

- [ ] **Step 5: 移除装柜从单一 SO 复制的逻辑**

`ContainerLoadsController.GenerateFromSo` 标记为兼容接口；新装柜从库存选择，`ContainerLoad.SummaryOrderId` 只读兼容。使用 `ContainerLoadSource` 和库存行追踪多张原汇总单。

- [ ] **Step 6: 测试和提交**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter ContainerConfirmationServiceTests
git add api
git commit -m "feat: confirm containers into receivables and shipments"
```

---

### Task 11: 分离物流服务商并升级出运费用双金额

**Files:**
- Create: `api/Futurem.Sourcing.Api/Entities/LogisticsProvider.cs`
- Create: `api/Futurem.Sourcing.Api/Controllers/LogisticsProvidersController.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/ShipmentExpense.cs`
- Modify: `api/Futurem.Sourcing.Api/Services/ShipmentExpenseService.cs`
- Modify: `api/Futurem.Sourcing.Api/Services/ShipmentFinanceSyncService.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/ShipmentExpensesController.cs`
- Modify: `api/Futurem.Sourcing.Api/Data/AppDbContext.cs`
- Modify: `api/Futurem.Sourcing.Api/Services/DatabaseUpgradeService.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/ShipmentLogisticsExpenseTests.cs`
- Create: `web/src/components/LogisticsExpenseEditor.vue`
- Create: `web/src/views/LogisticsProviders.vue`
- Modify: `web/src/views/Shipments.vue`

**Interfaces:**
- ShipmentExpense fields: `LogisticsProviderId`, `ServiceType`, `ProviderCost`, `CustomerCharge`, `ProfitAmount`。
- `ShipmentDepartureService` later consumes validated expenses。

- [ ] **Step 1: 写服务商分离和利润测试**

测试一个出运单三行费用可选择三个不同物流服务商；商品供应商 ID 不能作为物流服务商 ID 使用。断言 `ProfitAmount = CustomerCharge - ProviderCost`。

- [ ] **Step 2: 实现实体迁移**

保留旧 `SupplierId`、`Amount` 为兼容字段；新写入使用 `LogisticsProviderId`、`ProviderCost`、`CustomerCharge`。回填：旧 `Amount` 迁为 `ProviderCost`，`CustomerCharge` 初始等于旧金额并标记待复核。

- [ ] **Step 3: 改造费用校验**

费用大于 0 时物流服务商必填；所有金额强制人民币和两位小数。出运草稿可编辑，确认发运后只允许调整单。

- [ ] **Step 4: 改造前端**

费用表列为服务类型、物流服务商、供应商成本、客户收费、利润、备注；移除币种。

- [ ] **Step 5: 测试和提交**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter ShipmentLogisticsExpenseTests
cd web && npm test && npm run build
git add api web
git commit -m "feat: separate logistics providers and customer charges"
```

---

### Task 12: 实现确认发运、物流应付和客户应收追加

**Files:**
- Create: `api/Futurem.Sourcing.Api/Services/ShipmentDepartureService.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/ShipmentsController.cs`
- Modify: `api/Futurem.Sourcing.Api/Services/ShipmentFinanceSyncService.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/FinanceRecordLine.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/ShipmentDepartureServiceTests.cs`
- Modify: `web/src/views/Shipments.vue`
- Create: `web/src/components/FinanceRecordLines.vue`
- Modify: `web/src/views/FinanceRecords.vue`

**Interfaces:**
- `ConfirmDepartureAsync(long shipmentId, long? userId)`。
- Provider payable key: `shipment:{shipmentId}:expense:{expenseId}:provider`。
- Customer receivable line key: `shipment:{shipmentId}:expense:{expenseId}:customer`。

- [ ] **Step 1: 写追加同一应收测试**

装柜确认已有商品应收 100,000，客户已收 60,000；出运确认追加物流收费 5,000 后，同一应收头总额 105,000、已收仍为 60,000、未收 45,000。

- [ ] **Step 2: 写物流服务商应付测试**

三条费用两个服务商，应按每条费用生成来源唯一的应付行；重复确认不重复生成。

- [ ] **Step 3: 实现确认发运事务**

验证装柜关系、费用、柜号和实际离仓时间；状态改 `shipped`；生成/同步服务商应付；追加客户收费行；重新计算应收头；写审计。

- [ ] **Step 4: 替换旧 confirm/mark-shipped 行为**

保留兼容路由，但均调用 `ConfirmDepartureAsync`。草稿保存不生成物流财务。

- [ ] **Step 5: 前端显示统一应收明细**

按商品货款、海运费、报关费、拖车费、仓储费、其他费用分组；显示应收、已收、未收。

- [ ] **Step 6: 测试和提交**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter ShipmentDepartureServiceTests
cd web && npm test && npm run build
git add api web
git commit -m "feat: confirm departure into logistics finance"
```

---

### Task 13: 实现先进先出收付款、客户预收和供应商预付

**Files:**
- Create: `api/Futurem.Sourcing.Api/Entities/PaymentAllocation.cs`
- Create: `api/Futurem.Sourcing.Api/Entities/CustomerAdvance.cs`
- Create: `api/Futurem.Sourcing.Api/Entities/CustomerAdvanceUsage.cs`
- Create: `api/Futurem.Sourcing.Api/Services/FifoSettlementService.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/Payment.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/SupplierPrepayment.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/SupplierPrepaymentUsage.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/PaymentsController.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/FinanceRecordsController.cs`
- Modify: `api/Futurem.Sourcing.Api/Data/AppDbContext.cs`
- Modify: `api/Futurem.Sourcing.Api/Services/DatabaseUpgradeService.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/FifoSettlementServiceTests.cs`

**Interfaces:**
- `ApplyCustomerReceiptAsync(long paymentId)`。
- `ApplySupplierPaymentAsync(long paymentId, string counterpartyType)`。
- `ApplyAvailableCustomerAdvanceAsync(long customerId)`。
- `ApplyAvailableSupplierPrepaymentAsync(long counterpartyId, string counterpartyType)`。
- `ReversePaymentAsync(long paymentId, string reason, long? userId)`。

- [ ] **Step 1: 写客户 FIFO 测试**

三条应收 100、200、300，一笔收款 250：第一条结清，第二条已收 150，第三条未动。多收 700 时，剩余 100 形成客户预收。

- [ ] **Step 2: 写供应商 FIFO 和隔离测试**

商品供应商预付款不得抵扣物流服务商应付；同类型、同主体按最早应付抵扣。

- [ ] **Step 3: 实现分配流水**

每次冲销写 `PaymentAllocation`，包含付款、应收应付头、明细、金额、顺序和时间。金额变化使用 `RmbMoneyService.Round`。

- [ ] **Step 4: 实现预收预付自动抵扣**

新应收/应付创建后立即调用对应 `ApplyAvailable...`。按 `CreatedAt, Id` 排序，事务内锁定余额。

- [ ] **Step 5: 实现反冲**

反冲不删除原分配：创建负向分配和反冲付款，恢复明细余额与预收预付可用余额，并写原因和审计。

- [ ] **Step 6: 测试和提交**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter FifoSettlementServiceTests
git add api
git commit -m "feat: add fifo settlements and advances"
```

---

### Task 14: 实现财务调整单和验货/费用变更闭环

**Files:**
- Create: `api/Futurem.Sourcing.Api/Entities/FinancialAdjustment.cs`
- Create: `api/Futurem.Sourcing.Api/Controllers/FinancialAdjustmentsController.cs`
- Modify: `api/Futurem.Sourcing.Api/Services/FinanceDocumentService.cs`
- Modify: `api/Futurem.Sourcing.Api/Services/QcConfirmationService.cs`
- Modify: `api/Futurem.Sourcing.Api/Services/ShipmentDepartureService.cs`
- Modify: `api/Futurem.Sourcing.Api/Data/AppDbContext.cs`
- Modify: `api/Futurem.Sourcing.Api/Services/DatabaseUpgradeService.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/FinancialAdjustmentTests.cs`
- Create: `web/src/views/FinancialAdjustments.vue`

**Interfaces:**
- Adjustment types: `supplier_refund_or_credit`, `supplemental_payable`, `customer_receivable_adjustment`, `logistics_cost_adjustment`。
- `ApplyAsync(long adjustmentId, long? userId)` 幂等。

- [ ] **Step 1: 写已付款后验货减少测试**

原应付 1,000、已付 1,000，验货减少后应付应为 800：原付款保留，生成 200 供应商退款/抵扣调整，可用余额 200。

- [ ] **Step 2: 写出运费用确认后修改测试**

确认发运后的费用实体不得直接 PUT；必须创建调整单，原费用和财务来源保留。

- [ ] **Step 3: 实现调整状态机**

`draft → approved → applied`，取消状态 `cancelled`。应用时创建正负财务明细，不覆盖原明细。

- [ ] **Step 4: 实现页面和权限**

页面显示来源单据、原金额、调整金额、原因、审批人、应用结果；只有 `finance.adjust` 权限可应用。

- [ ] **Step 5: 测试和提交**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter FinancialAdjustmentTests
cd web && npm test && npm run build
git add api web
git commit -m "feat: add auditable financial adjustments"
```

---

### Task 15: 实现标签、唛头、ZIP 和供应商门户下载日志

**Files:**
- Create: `api/Futurem.Sourcing.Api/Entities/PrintPackagePublication.cs`
- Create: `api/Futurem.Sourcing.Api/Entities/PrintPackageDownloadLog.cs`
- Create: `api/Futurem.Sourcing.Api/Services/LabelMarkPackageService.cs`
- Create: `api/Futurem.Sourcing.Api/Controllers/SupplierPortalController.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/PrintTemplate.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/PrintCenterController.cs`
- Modify: `api/Futurem.Sourcing.Api/Data/AppDbContext.cs`
- Modify: `api/Futurem.Sourcing.Api/Services/DatabaseUpgradeService.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/LabelMarkPackageServiceTests.cs`
- Modify: `web/src/views/PrintCenter.vue`
- Create: `web/src/views/SupplierPortal.vue`

**Interfaces:**
- `GenerateAsync(string sourceType, long sourceId, IReadOnlyDictionary<long, int> labelCopies, IReadOnlyDictionary<long, int> markCopies, long? userId)` returns ZIP bytes and manifest hash。
- `PublishAsync(long packageId, long supplierId, long? userId)`。
- `RecordDownloadAsync(long publicationId, string fileType, string? ipAddress)`。

- [ ] **Step 1: 写模板范围和数量测试**

同一 CO/PO 所有商品必须使用同一标签模板和唛头模板。默认标签份数等于件数，唛头份数等于箱数；允许只调整份数。

- [ ] **Step 2: 写快照稳定测试**

订单确认后修改模板主数据，重新生成历史订单资料包，输出内容哈希必须与确认快照一致。

- [ ] **Step 3: 实现 PDF/Excel/ZIP 生成**

ZIP 固定包含：

```text
product-labels.pdf
carton-marks.pdf
print-quantities.xlsx
manifest.json
```

`manifest.json` 包含来源单号、模板快照哈希、生成时间和每条打印份数。

- [ ] **Step 4: 实现门户令牌和下载日志**

发布生成不可猜测令牌；供应商只能访问其发布包。每次下载记录文件类型、时间、IP 和次数。

- [ ] **Step 5: 测试和提交**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter LabelMarkPackageServiceTests
cd web && npm test && npm run build
git add api web
git commit -m "feat: add supplier label and mark packages"
```

---

### Task 16: 权限、菜单、全局搜索和中文业务名称

**Files:**
- Modify: `api/Futurem.Sourcing.Api/Controllers/RbacController.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/GlobalSearchController.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/BusinessDashboardController.cs`
- Modify: `web/src/layouts/MainLayout.vue`
- Modify: `web/src/router.ts`
- Modify: `web/src/views/Suppliers.vue`
- Delete route only: `web/src/views/Markets.vue` remains in source for rollback but is not imported or routed
- Create: `api/Futurem.Sourcing.Api.Tests/Controllers/RbacWorkflowPermissionTests.cs`

**Interfaces:**
- Permissions exactly match design spec section 21。
- Global search routes new entity types to real pages。

- [ ] **Step 1: 写权限种子失败测试**

断言 `qc.unlock`、`container_load.confirm`、`shipment.confirm_departure`、`finance.adjust`、`payment.reverse`、`supplier_portal.publish` 均存在。

- [ ] **Step 2: 更新权限种子和后端授权**

每个命令接口使用策略或统一权限服务校验，不能只隐藏前端按钮。

- [ ] **Step 3: 整理菜单**

删除市场入口；Products 改客户历史商品；SO 改客户汇总单；Suppliers 改商品供应商；新增进口商、模板、送货通知、仓库库存、物流服务商、财务调整。

- [ ] **Step 4: 更新全局搜索**

搜索范围增加客户货号、客户条码、订单商品、送货通知、库存批次和物流服务商；提示文字不再显示 SO 和币种。

- [ ] **Step 5: 测试和提交**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter RbacWorkflowPermissionTests
cd web && npm test && npm run build
git add api web
git commit -m "feat: align navigation search and permissions"
```

---

### Task 17: 旧数据回填、兼容和升级健康检查

**Files:**
- Modify: `api/Futurem.Sourcing.Api/Services/DatabaseUpgradeService.cs`
- Create: `api/Futurem.Sourcing.Api/Services/LegacyOrderProductBackfillService.cs`
- Create: `api/Futurem.Sourcing.Api/Services/LegacyFinanceBackfillService.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/LegacyBackfillTests.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/MonitorCenterController.cs`
- Modify: `docs/03-BusinessFlow.md`

**Interfaces:**
- `LegacyOrderProductBackfillService.RunBatchAsync(int batchSize, long? afterDocumentLineId)`。
- `LegacyFinanceBackfillService.RunBatchAsync(int batchSize, long? afterFinanceRecordId)`。
- Health report includes counts for unmapped lines, invalid barcodes, orphan finance records and expired reservations。

- [ ] **Step 1: 写旧 DocumentLine 回填测试**

CO/PO 旧行应生成 `OrderProduct` 快照并回填 `OrderProductId`。无法确定客户条码时使用旧条码或 SKU，并设置 `NeedsReview` 标记。

- [ ] **Step 2: 写人民币归一测试**

所有客户、CO、PO、汇总、出运、费用、财务、付款和预付款兼容币种均更新为 `RMB`。

- [ ] **Step 3: 实现分批回填**

每批最多 500 行，按主键游标前进，每批独立事务；重复运行跳过已回填行。

- [ ] **Step 4: 实现升级健康报告**

监控中心返回：新表存在、目标版本、回填进度、未映射数量、库存负数、重复来源键、过期未释放锁定。

- [ ] **Step 5: 测试和提交**

```bash
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter LegacyBackfillTests
git add api docs
git commit -m "feat: backfill legacy sourcing data safely"
```

---

### Task 18: 完整业务链 E2E、检查脚本和发布门禁

**Files:**
- Create: `web/playwright.config.ts`
- Create: `web/e2e/order-to-shipment.spec.ts`
- Create: `api/Futurem.Sourcing.Api.Tests/Integration/IdempotencyWorkflowTests.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Integration/ConcurrentReservationTests.cs`
- Modify: `web/package.json`
- Modify: `scripts/check.sh`
- Modify: `README.md`
- Modify: `docs/03-BusinessFlow.md`

**Interfaces:**
- E2E fixture creates customer, importer, templates, product supplier, logistics provider and warehouse。
- Workflow must run against MySQL test container, not only EF InMemory。

- [ ] **Step 1: 写完整 E2E**

自动执行：

```text
创建 CO 和订单商品
→ 确认 CO
→ 整商品生成 PO
→ PO 商品按整箱加入汇总
→ 确认汇总并生成送货通知
→ 分批收货
→ 验货确认实际接受
→ 查看库存
→ 装柜锁定
→ 实际装柜少于库存
→ 确认装柜并生成商品应收和出运草稿
→ 确认发运并追加物流应收/应付
→ 客户收款 FIFO
→ 供应商付款 FIFO
→ 验证留仓库存可进入下一装柜单
```

- [ ] **Step 2: 写并发测试**

两个 DbContext 同时预留同一 PO 箱数、同时锁定同一库存，最终只能一个成功，另一个返回业务冲突且数据库不超量。

- [ ] **Step 3: 写幂等测试**

重复确认 QC、装柜和出运各 3 次，库存流水、应收、应付、出运单数量均保持 1 份业务结果。

- [ ] **Step 4: 更新脚本**

`web/package.json` 增加：

```json
{
  "scripts": {
    "test:e2e": "playwright test"
  }
}
```

`scripts/check.sh` 顺序固定：restore → API build → API unit/integration → web install → web unit → web build；E2E 在 CI 的 MySQL 服务启动后执行。

- [ ] **Step 5: 运行最终验证**

```bash
./scripts/check.sh
cd web && npm run test:e2e
```

Expected:

- API build PASS
- API unit/integration PASS
- Web unit PASS
- Web typecheck/build PASS
- E2E PASS
- 数据升级健康报告无阻断错误

- [ ] **Step 6: 提交**

```bash
git add api web scripts README.md docs
git commit -m "test: verify complete order to shipment workflow"
```

---

## 实施顺序和发布门禁

1. Task 1–4：订单商品基础；发布后仍兼容旧业务。
2. Task 5–7：汇总、送货、收货、验货；先在测试客户启用。
3. Task 8–10：仓库库存和装柜；完成库存盘点后切换。
4. Task 11–14：出运和财务；对账通过后关闭旧自动财务入口。
5. Task 15–17：供应商资料包、权限、旧数据回填。
6. Task 18：全量验收后再设为默认流程。

每阶段发布前必须满足：

- `./scripts/check.sh` 通过。
- 数据库升级可重复执行。
- 新增命令接口幂等测试通过。
- 关键数量和金额对账无差异。
- 回滚不要求删除新表；通过功能开关恢复旧读取路径。
