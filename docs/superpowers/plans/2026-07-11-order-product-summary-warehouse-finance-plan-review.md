# 实施计划自审与执行修正

日期：2026-07-11  
状态：已完成自审  
适用计划：`2026-07-11-order-product-summary-warehouse-finance-implementation.md`

本文件是实施计划的强制执行补充；与主计划冲突时，以本文件为准。

## 1. 规格覆盖检查

已确认以下需求均有对应任务：

| 规格要求 | 对应任务 |
|---|---|
| 订单商品独立快照、历史复制 | Task 2–4 |
| 一条订单商品全部数量进入一张有效 PO | Task 3 |
| 同订单统一进口商、标签、唛头模板 | Task 2–4、Task 15 |
| PO 按整箱拆分汇总且并发不超量 | Task 5 |
| 送货通知与分批收货 | Task 6 |
| 一收货一验货、最终接受数量生成应付 | Task 7、Task 9 |
| 仓库、库位、库存批次和流水 | Task 8 |
| 装柜锁定 72 小时、不因保存续期 | Task 9 |
| 实际装柜出库、剩余商品继续留仓 | Task 10 |
| 原汇总单已装柜且不可再次装柜 | Task 10 |
| 一柜一出运单 | Task 10、Task 12 |
| 商品应收与物流费用追加同一应收 | Task 10、Task 12 |
| 商品供应商和物流服务商分离 | Task 11–13 |
| 客户/供应商 FIFO、预收预付 | Task 13 |
| 验货解锁、财务调整、付款反冲 | Task 7、Task 13–14 |
| 标签、唛头、Excel、ZIP 和下载日志 | Task 15 |
| 人民币单币种、市场菜单删除 | Task 1、Task 4、Task 11、Task 16–17 |
| 权限、审计、升级、E2E | Task 1、Task 16–18 |

未发现规格遗漏。

## 2. 强制任务依赖顺序

主计划的任务编号保留，但执行顺序必须调整为：

```text
Task 1
→ Task 2
→ Task 3
→ Task 4
→ Task 5
→ Task 6
→ Task 8
→ Task 7
→ Task 9
→ Task 10
→ Task 11
→ Task 12
→ Task 13
→ Task 14
→ Task 15
→ Task 16
→ Task 17
→ Task 18
```

原因：Task 7 的验货确认需要消费 Task 8 产生的 `InventoryService`、`InventoryLot` 和库存流水接口。不得在库存基础完成前实现验货确认入库。

## 3. 财务基础依赖修正

Task 7 在生成供应商应付前，必须先创建以下最小财务基础文件；这些内容从 Task 10 前移到 Task 7 的第一个实现步骤：

- `api/Futurem.Sourcing.Api/Entities/FinanceRecordLine.cs`
- `api/Futurem.Sourcing.Api/Services/FinanceDocumentService.cs`

固定接口：

```csharp
public sealed record FinanceLineInput(
    string SourceKey,
    string LineType,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount,
    string? Description);

public sealed class FinanceDocumentService
{
    public Task<FinanceRecord> EnsurePayableAsync(
        string sourceKey,
        string targetType,
        long targetId,
        long supplierId,
        IReadOnlyCollection<FinanceLineInput> lines,
        CancellationToken cancellationToken = default);

    public Task<FinanceRecord> EnsureReceivableAsync(
        string sourceKey,
        string targetType,
        long targetId,
        long customerId,
        IReadOnlyCollection<FinanceLineInput> lines,
        CancellationToken cancellationToken = default);

    public Task RecalculateAsync(
        long financeRecordId,
        CancellationToken cancellationToken = default);
}
```

Task 10 只扩展此服务以支持装柜商品应收和出运物流费用追加，不得重新创建第二个同名服务。

## 4. AuditLog 类型一致性修正

Task 1 必须把 `api/Futurem.Sourcing.Api/Entities/AuditLog.cs` 加入修改文件。

现有字段继续使用：

- `TargetType`
- `TargetId`
- `BeforeJson`
- `AfterJson`
- `UserId`

新增：

```csharp
public string? Reason { get; set; }
public string? CorrelationId { get; set; }
public string? SourceDocumentType { get; set; }
public long? SourceDocumentId { get; set; }
```

`AuditTrailService.WriteAsync` 必须写入现有字段，不得引用不存在的 `EntityType` 或 `EntityId`：

```csharp
_db.AuditLogs.Add(new AuditLog
{
    UserId = userId,
    Username = userId?.ToString() ?? "system",
    Action = action,
    Module = entityType,
    TargetType = entityType,
    TargetId = entityId,
    TargetNo = entityId.ToString(),
    BeforeJson = before is null ? string.Empty : JsonSerializer.Serialize(before),
    AfterJson = after is null ? string.Empty : JsonSerializer.Serialize(after),
    Reason = reason,
    CorrelationId = correlationId ?? Guid.NewGuid().ToString("N"),
    Result = "success",
    CreatedAt = DateTime.Now
});
```

## 5. CO 生成 PO 接口类型修正

Task 3 的固定接口改为：

```csharp
public sealed record GeneratePoItem(long OrderProductId, decimal Quantity);

public Task<PurchaseOrder> GeneratePoAsync(
    long orderId,
    IReadOnlyCollection<GeneratePoItem> items,
    long supplierId,
    DateTime? expectedDeliveryDate,
    long? userId);
```

不得同时保留 `IReadOnlyCollection<long>` 和 `IReadOnlyCollection<GeneratePoItem>` 两种签名。

校验：

```csharp
if (item.Quantity != remainingQuantity)
    throw new BusinessRuleException(
        "ORDER_PRODUCT_MUST_CONVERT_FULL_QUANTITY",
        "订单商品必须以全部未采购数量进入一张采购订单");
```

## 6. 标签和唛头文件依赖

Task 15 修改文件中增加：

- `api/Futurem.Sourcing.Api/Futurem.Sourcing.Api.csproj`

实现固定采用：

- `System.IO.Compression.ZipArchive` 生成 ZIP。
- `ClosedXML` 生成 `print-quantities.xlsx`。
- `QuestPDF` 生成标签和唛头 PDF。

安装命令必须在执行时锁定兼容 .NET 9 的明确版本，并把版本写入 csproj；执行人员在安装前通过 NuGet 官方源确认版本，不使用预览包。

## 7. MySQL 并发测试要求

EF Core InMemory 不支持真实事务隔离和行锁，因此以下测试必须在 MySQL 测试容器运行：

- 同一 PO 行并发汇总预留。
- 同一库存批次并发装柜锁定。
- FIFO 自动抵扣的并发余额更新。
- 装柜确认、验货确认和出运确认的重复请求。

InMemory 测试只验证纯业务计算和单服务行为，不能作为并发通过依据。

## 8. 占位符扫描

已扫描主计划，没有 `TBD`、`TODO`、`implement later` 或“类似 Task N”的占位表达。

执行时，主计划中描述实体字段但未完整展开的步骤，必须以已确认设计规格中的字段清单为完整输入，不允许省略字段或自行改变名称。

## 9. 规格优先级

执行时读取顺序：

1. `docs/superpowers/specs/2026-07-11-order-product-summary-warehouse-finance-design.md`
2. `docs/superpowers/specs/2026-07-11-order-product-summary-warehouse-finance-design-amendments.md`
3. `docs/superpowers/plans/2026-07-11-order-product-summary-warehouse-finance-implementation.md`
4. 本自审文件

同层冲突时，以用户最后确认的业务规则和本自审修正为准。