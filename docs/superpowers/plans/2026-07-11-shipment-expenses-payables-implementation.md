# 出运费用与供应商应付联动 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在出运单中增加固定与自定义费用，确认后自动生成供应商应付，支持超付转预付款、同供应商同币种自动抵扣、体积重量汇总、两位小数精度以及商品条码可修改。

**Architecture:** 以 `ShipmentExpense` 为出运费用主记录，每条费用一对一关联 `FinanceRecord`。`ShipmentFinanceSyncService` 负责幂等同步应付，`SupplierPrepaymentService` 负责预付款生成、释放与抵扣，所有确认/出运/同步操作在同一数据库事务中完成。前端在现有 `Shipments.vue` 中增加费用表、计算值/最终值和标准状态操作。

**Tech Stack:** .NET 9、ASP.NET Core Web API、Entity Framework Core 9、Pomelo MySQL、Vue 3、TypeScript、Element Plus、Node 20、Docker Compose、xUnit、EF Core InMemory。

## Global Constraints

- 一张出运单只能使用一种币种。
- 固定费用为海运费、仓库费、装卸费、内陆费，每种费用只能一行。
- 自定义费用按标准化名称唯一，同名不能重复。
- 费用金额大于 0 时必须选择供应商。
- 草稿不生成应付；`confirmed` 或 `shipped` 时生成并同步。
- 确认后允许修改费用并同步原应付，不得重复生成。
- 超付部分单独生成供应商预付款。
- 新应付自动优先抵扣同供应商、同币种预付款。
- 金额、立方数、毛重、净重统一使用 `decimal(18,2)` 和两位小数。
- 商品条码允许修改，但必须非空、最多 80 字符、全系统唯一；已删除商品条码仍占用。
- 所有财务同步必须幂等且在单个数据库事务内完成。
- 不实现多币种费用拆分、同类费用多行、跨币种预付款折算、总账凭证自动生成。

---

## 文件结构与责任边界

### 新增后端文件

- `api/Futurem.Sourcing.Api/Entities/ShipmentExpense.cs`：出运费用实体。
- `api/Futurem.Sourcing.Api/Entities/SupplierPrepayment.cs`：供应商预付款实体。
- `api/Futurem.Sourcing.Api/Entities/SupplierPrepaymentUsage.cs`：预付款抵扣流水实体。
- `api/Futurem.Sourcing.Api/Services/FinanceBalanceService.cs`：统一计算应付未付、状态和两位小数。
- `api/Futurem.Sourcing.Api/Services/ShipmentMeasurementService.cs`：汇总出运明细体积、毛重、净重。
- `api/Futurem.Sourcing.Api/Services/ShipmentExpenseService.cs`：初始化与校验出运费用。
- `api/Futurem.Sourcing.Api/Services/SupplierPrepaymentService.cs`：生成、释放、抵扣预付款。
- `api/Futurem.Sourcing.Api/Services/ShipmentFinanceSyncService.cs`：出运费用与应付同步。
- `api/Futurem.Sourcing.Api/Controllers/ShipmentExpensesController.cs`：费用 CRUD。
- `api/Futurem.Sourcing.Api/Controllers/SupplierPrepaymentsController.cs`：预付款查询。
- `api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj`：API 测试工程。
- `api/Futurem.Sourcing.Api.Tests/TestDbFactory.cs`：内存数据库测试基座。
- `api/Futurem.Sourcing.Api.Tests/Services/*.cs`：服务单元测试。
- `api/Futurem.Sourcing.Api.Tests/Controllers/ProductsControllerTests.cs`：条码修改测试。

### 修改后端文件

- `api/Futurem.Sourcing.Api/Entities/Shipment.cs`：币种、计算值、最终值、费用总额和同步状态。
- `api/Futurem.Sourcing.Api/Entities/FinanceRecord.cs`：费用关联、预付款抵扣、超付转出字段。
- `api/Futurem.Sourcing.Api/Entities/Product.cs`：数值精度调整。
- `api/Futurem.Sourcing.Api/Entities/DocumentLine.cs`：金额与体积重量精度调整。
- `api/Futurem.Sourcing.Api/Entities/Payment.cs`：金额精度调整。
- `api/Futurem.Sourcing.Api/Data/AppDbContext.cs`：DbSet、表名、索引、关系。
- `api/Futurem.Sourcing.Api/Services/DatabaseUpgradeService.cs`：现有数据库升级。
- `api/Futurem.Sourcing.Api/Controllers/ShipmentsController.cs`：确认、已出运、重新计算、财务同步。
- `api/Futurem.Sourcing.Api/Controllers/FinanceRecordsController.cs`：余额计算纳入预付款抵扣和转出。
- `api/Futurem.Sourcing.Api/Controllers/PaymentsController.cs`：状态计算统一化。
- `api/Futurem.Sourcing.Api/Controllers/ProductsController.cs`：允许修改条码并校验唯一。
- `api/Futurem.Sourcing.Api/Program.cs`：注册服务。
- `scripts/check.sh`：加入 API 测试和 Web 测试。

### 新增前端文件

- `web/src/utils/shipmentFinance.ts`：两位小数、费用名称标准化、费用余额显示计算。
- `web/tests/shipmentFinance.test.ts`：前端纯函数测试。
- `web/src/components/ShipmentExpensesEditor.vue`：费用表格与 CRUD。
- `web/src/components/ShipmentMeasurements.vue`：计算值和最终值编辑。

### 修改前端文件

- `web/src/views/Shipments.vue`：整合费用、体积重量、状态按钮与同步状态。
- `web/src/views/FinanceRecords.vue`：显示预付款抵扣、实际未付和出运费用来源。
- `web/package.json`：测试脚本覆盖两个测试文件。

---

### Task 1: 建立后端测试基座与统一财务计算

**Files:**
- Create: `api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj`
- Create: `api/Futurem.Sourcing.Api.Tests/TestDbFactory.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/FinanceBalanceServiceTests.cs`
- Create: `api/Futurem.Sourcing.Api/Services/FinanceBalanceService.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/FinanceRecord.cs`

**Interfaces:**
- Produces: `FinanceBalanceService.Round2(decimal)`, `FinanceBalanceService.EffectiveSettled(FinanceRecord)`, `FinanceBalanceService.Outstanding(FinanceRecord)`, `FinanceBalanceService.RefreshStatus(FinanceRecord)`。
- Later tasks must use these methods instead of duplicating余额和状态公式。

- [ ] **Step 1: 创建测试工程**

`Futurem.Sourcing.Api.Tests.csproj` 使用以下完整内容：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="9.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../Futurem.Sourcing.Api/Futurem.Sourcing.Api.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: 创建内存 DbContext 工厂**

```csharp
using Futurem.Sourcing.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Tests;

public static class TestDbFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }
}
```

- [ ] **Step 3: 扩展 FinanceRecord 并写失败测试**

在 `FinanceRecord` 增加：

```csharp
public long? ShipmentExpenseId { get; set; }

[Column(TypeName = "decimal(18,2)")]
public decimal PrepaymentAppliedAmount { get; set; }

[Column(TypeName = "decimal(18,2)")]
public decimal OverpaymentTransferredAmount { get; set; }

public string? SourceKey { get; set; }
```

将 `Amount`、`PaidAmount` 改为 `decimal(18,2)`。

测试内容：

```csharp
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;

namespace Futurem.Sourcing.Api.Tests.Services;

public class FinanceBalanceServiceTests
{
    [Fact]
    public void Outstanding_SubtractsPaymentsCreditsAndTransferredOverpayment()
    {
        var record = new FinanceRecord
        {
            Amount = 10000m,
            PaidAmount = 6000m,
            PrepaymentAppliedAmount = 2000m,
            OverpaymentTransferredAmount = 500m
        };

        Assert.Equal(2500m, FinanceBalanceService.Outstanding(record));
    }

    [Theory]
    [InlineData(0, 0, 0, "pending")]
    [InlineData(100, 20, 0, "partial")]
    [InlineData(100, 100, 0, "done")]
    public void RefreshStatus_UsesEffectiveSettled(decimal amount, decimal paid, decimal credit, string expected)
    {
        var record = new FinanceRecord { Amount = amount, PaidAmount = paid, PrepaymentAppliedAmount = credit };
        FinanceBalanceService.RefreshStatus(record);
        Assert.Equal(expected, record.Status);
    }
}
```

- [ ] **Step 4: 运行测试确认失败**

Run:

```bash
cd api
dotnet test Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter FinanceBalanceServiceTests
```

Expected: FAIL，提示 `FinanceBalanceService` 不存在。

- [ ] **Step 5: 实现统一计算服务**

```csharp
using Futurem.Sourcing.Api.Entities;

namespace Futurem.Sourcing.Api.Services;

public static class FinanceBalanceService
{
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal EffectiveSettled(FinanceRecord record)
        => Round2(record.PaidAmount + record.PrepaymentAppliedAmount - record.OverpaymentTransferredAmount);

    public static decimal Outstanding(FinanceRecord record)
        => Math.Max(0m, Round2(record.Amount - EffectiveSettled(record)));

    public static void RefreshStatus(FinanceRecord record)
    {
        record.Amount = Round2(record.Amount);
        record.PaidAmount = Round2(record.PaidAmount);
        record.PrepaymentAppliedAmount = Round2(record.PrepaymentAppliedAmount);
        record.OverpaymentTransferredAmount = Round2(record.OverpaymentTransferredAmount);
        var settled = EffectiveSettled(record);
        record.Status = settled <= 0m ? "pending" : settled < record.Amount ? "partial" : "done";
    }
}
```

- [ ] **Step 6: 运行测试确认通过**

Run:

```bash
dotnet test Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter FinanceBalanceServiceTests
```

Expected: PASS。

- [ ] **Step 7: 提交**

```bash
git add api/Futurem.Sourcing.Api.Tests api/Futurem.Sourcing.Api/Entities/FinanceRecord.cs api/Futurem.Sourcing.Api/Services/FinanceBalanceService.cs
git commit -m "test: add finance balance test foundation"
```

---

### Task 2: 新增出运费用与供应商预付款数据模型

**Files:**
- Create: `api/Futurem.Sourcing.Api/Entities/ShipmentExpense.cs`
- Create: `api/Futurem.Sourcing.Api/Entities/SupplierPrepayment.cs`
- Create: `api/Futurem.Sourcing.Api/Entities/SupplierPrepaymentUsage.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/Shipment.cs`
- Modify: `api/Futurem.Sourcing.Api/Data/AppDbContext.cs`
- Modify: `api/Futurem.Sourcing.Api/Services/DatabaseUpgradeService.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/ShipmentSchemaTests.cs`

**Interfaces:**
- Produces DbSets: `ShipmentExpenses`, `SupplierPrepayments`, `SupplierPrepaymentUsages`。
- Produces unique keys: fixed expense `ShipmentId + ExpenseCode`; custom expense `ShipmentId + NormalizedExpenseName`；finance `ShipmentExpenseId` unique。

- [ ] **Step 1: 写模型元数据失败测试**

```csharp
using Futurem.Sourcing.Api.Entities;

namespace Futurem.Sourcing.Api.Tests.Services;

public class ShipmentSchemaTests
{
    [Fact]
    public void ShipmentExpense_DefaultsAreStable()
    {
        var expense = new ShipmentExpense();
        Assert.Equal("OTHER", expense.ExpenseCode);
        Assert.Equal("pending", expense.FinanceStatus);
        Assert.Equal("RMB", expense.Currency);
    }

    [Fact]
    public void Shipment_DefaultCurrencyAndFinanceStatusAreStable()
    {
        var shipment = new Shipment();
        Assert.Equal("RMB", shipment.Currency);
        Assert.Equal("not_synced", shipment.FinanceSyncStatus);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run:

```bash
cd api
dotnet test Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter ShipmentSchemaTests
```

Expected: FAIL，缺少实体和属性。

- [ ] **Step 3: 创建三个实体**

`ShipmentExpense.cs`：

```csharp
using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class ShipmentExpense : BaseEntity
{
    public long ShipmentId { get; set; }
    public string ExpenseCode { get; set; } = "OTHER";
    public string ExpenseName { get; set; } = string.Empty;
    public string NormalizedExpenseName { get; set; } = string.Empty;
    public bool IsCustom { get; set; }
    public long? SupplierId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "RMB";
    public long? FinanceRecordId { get; set; }
    public string FinanceStatus { get; set; } = "pending";
    public int SortNo { get; set; }
}
```

`SupplierPrepayment.cs`：

```csharp
using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class SupplierPrepayment : BaseEntity
{
    public string No { get; set; } = string.Empty;
    public long SupplierId { get; set; }
    public string Currency { get; set; } = "RMB";

    [Column(TypeName = "decimal(18,2)")]
    public decimal OriginalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AvailableAmount { get; set; }

    public string SourceType { get; set; } = "SHIPMENT_EXPENSE_OVERPAYMENT";
    public long SourceId { get; set; }
    public long? SourceFinanceRecordId { get; set; }
    public string Status { get; set; } = "available";
}
```

`SupplierPrepaymentUsage.cs`：

```csharp
using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class SupplierPrepaymentUsage : BaseEntity
{
    public long SupplierPrepaymentId { get; set; }
    public long FinanceRecordId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public string UsageType { get; set; } = "apply";
}
```

- [ ] **Step 4: 扩展 Shipment**

增加以下属性，所有数值字段使用 `decimal(18,2)`：

```csharp
public string Currency { get; set; } = "RMB";
[Column(TypeName = "decimal(18,2)")] public decimal CalculatedTotalCbm { get; set; }
[Column(TypeName = "decimal(18,2)")] public decimal FinalTotalCbm { get; set; }
[Column(TypeName = "decimal(18,2)")] public decimal CalculatedGrossWeightKg { get; set; }
[Column(TypeName = "decimal(18,2)")] public decimal FinalGrossWeightKg { get; set; }
[Column(TypeName = "decimal(18,2)")] public decimal CalculatedNetWeightKg { get; set; }
[Column(TypeName = "decimal(18,2)")] public decimal FinalNetWeightKg { get; set; }
[Column(TypeName = "decimal(18,2)")] public decimal ExpenseTotal { get; set; }
public string FinanceSyncStatus { get; set; } = "not_synced";
public string? FinanceSyncMessage { get; set; }
public DateTime? FinanceSyncedAt { get; set; }
```

同时在文件顶部加入：

```csharp
using System.ComponentModel.DataAnnotations.Schema;
```

- [ ] **Step 5: 配置 AppDbContext**

加入 DbSet：

```csharp
public DbSet<ShipmentExpense> ShipmentExpenses => Set<ShipmentExpense>();
public DbSet<SupplierPrepayment> SupplierPrepayments => Set<SupplierPrepayment>();
public DbSet<SupplierPrepaymentUsage> SupplierPrepaymentUsages => Set<SupplierPrepaymentUsage>();
```

加入表名与索引：

```csharp
modelBuilder.Entity<ShipmentExpense>().ToTable("shipment_expenses");
modelBuilder.Entity<SupplierPrepayment>().ToTable("supplier_prepayments");
modelBuilder.Entity<SupplierPrepaymentUsage>().ToTable("supplier_prepayment_usages");
modelBuilder.Entity<ShipmentExpense>().HasIndex(x => new { x.ShipmentId, x.ExpenseCode }).IsUnique();
modelBuilder.Entity<ShipmentExpense>().HasIndex(x => new { x.ShipmentId, x.NormalizedExpenseName }).IsUnique();
modelBuilder.Entity<FinanceRecord>().HasIndex(x => x.ShipmentExpenseId).IsUnique();
modelBuilder.Entity<SupplierPrepayment>().HasIndex(x => new { x.SupplierId, x.Currency, x.Status });
modelBuilder.Entity<SupplierPrepaymentUsage>().HasIndex(x => new { x.SupplierPrepaymentId, x.FinanceRecordId });
```

固定费用使用非空 `ExpenseCode`，自定义费用使用唯一代码 `CUSTOM:<NORMALIZED_NAME>`，因此两个唯一索引不会因空值产生冲突。

- [ ] **Step 6: 扩展 DatabaseUpgradeService**

将 `TargetVersion` 改为 `1.1.0`，在 `UpgradeAsync()` 中调用：

```csharp
await EnsureShipmentFinanceSchemaAsync();
await EnsureTwoDecimalPrecisionAsync();
```

新增方法必须执行：

```csharp
private async Task EnsureShipmentFinanceSchemaAsync()
{
    await _db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS `shipment_expenses` (
          `id` BIGINT NOT NULL AUTO_INCREMENT,
          `shipment_id` BIGINT NOT NULL,
          `expense_code` VARCHAR(120) NOT NULL,
          `expense_name` VARCHAR(200) NOT NULL,
          `normalized_expense_name` VARCHAR(200) NOT NULL,
          `is_custom` TINYINT(1) NOT NULL DEFAULT 0,
          `supplier_id` BIGINT NULL,
          `amount` DECIMAL(18,2) NOT NULL DEFAULT 0,
          `currency` VARCHAR(20) NOT NULL DEFAULT 'RMB',
          `finance_record_id` BIGINT NULL,
          `finance_status` VARCHAR(40) NOT NULL DEFAULT 'pending',
          `sort_no` INT NOT NULL DEFAULT 0,
          `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
          `created_by` BIGINT NULL,
          `created_at` DATETIME NOT NULL,
          `updated_by` BIGINT NULL,
          `updated_at` DATETIME NULL,
          `remark` TEXT NULL,
          PRIMARY KEY (`id`),
          UNIQUE KEY `ux_shipment_expense_code` (`shipment_id`,`expense_code`),
          UNIQUE KEY `ux_shipment_expense_name` (`shipment_id`,`normalized_expense_name`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """);

    await _db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS `supplier_prepayments` (
          `id` BIGINT NOT NULL AUTO_INCREMENT,
          `no` VARCHAR(80) NOT NULL,
          `supplier_id` BIGINT NOT NULL,
          `currency` VARCHAR(20) NOT NULL,
          `original_amount` DECIMAL(18,2) NOT NULL DEFAULT 0,
          `available_amount` DECIMAL(18,2) NOT NULL DEFAULT 0,
          `source_type` VARCHAR(80) NOT NULL,
          `source_id` BIGINT NOT NULL,
          `source_finance_record_id` BIGINT NULL,
          `status` VARCHAR(40) NOT NULL DEFAULT 'available',
          `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
          `created_by` BIGINT NULL,
          `created_at` DATETIME NOT NULL,
          `updated_by` BIGINT NULL,
          `updated_at` DATETIME NULL,
          `remark` TEXT NULL,
          PRIMARY KEY (`id`),
          KEY `ix_supplier_prepayment_available` (`supplier_id`,`currency`,`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """);

    await _db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS `supplier_prepayment_usages` (
          `id` BIGINT NOT NULL AUTO_INCREMENT,
          `supplier_prepayment_id` BIGINT NOT NULL,
          `finance_record_id` BIGINT NOT NULL,
          `amount` DECIMAL(18,2) NOT NULL DEFAULT 0,
          `usage_type` VARCHAR(40) NOT NULL DEFAULT 'apply',
          `is_deleted` TINYINT(1) NOT NULL DEFAULT 0,
          `created_by` BIGINT NULL,
          `created_at` DATETIME NOT NULL,
          `updated_by` BIGINT NULL,
          `updated_at` DATETIME NULL,
          `remark` TEXT NULL,
          PRIMARY KEY (`id`),
          KEY `ix_prepayment_usage_source` (`supplier_prepayment_id`,`finance_record_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """);
}
```

使用现有 `AddColumnIfMissingAsync` 为 `shipments` 和 `finance_records` 增加本任务定义的全部列；使用 `ALTER TABLE ... MODIFY COLUMN ... DECIMAL(18,2)` 调整精度。每条 SQL 单独执行，避免 MySQL 驱动禁用多语句时失败。

- [ ] **Step 7: 运行测试和编译**

```bash
cd api
dotnet test Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter ShipmentSchemaTests
dotnet build Futurem.Sourcing.Api/Futurem.Sourcing.Api.csproj -c Release
```

Expected: 全部 PASS / Build succeeded。

- [ ] **Step 8: 提交**

```bash
git add api/Futurem.Sourcing.Api/Entities api/Futurem.Sourcing.Api/Data/AppDbContext.cs api/Futurem.Sourcing.Api/Services/DatabaseUpgradeService.cs api/Futurem.Sourcing.Api.Tests/Services/ShipmentSchemaTests.cs
git commit -m "feat: add shipment expense and supplier prepayment schema"
```

---

### Task 3: 实现出运体积重量自动汇总

**Files:**
- Create: `api/Futurem.Sourcing.Api/Services/ShipmentMeasurementService.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/ShipmentMeasurementServiceTests.cs`
- Modify: `api/Futurem.Sourcing.Api/Program.cs`

**Interfaces:**
- Produces: `Task<ShipmentMeasurementResult> CalculateAsync(long shipmentId)`。
- Produces: `Task<Shipment> RecalculateAsync(long shipmentId, bool overwriteFinalValues)`。

- [ ] **Step 1: 写失败测试**

```csharp
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;

namespace Futurem.Sourcing.Api.Tests.Services;

public class ShipmentMeasurementServiceTests
{
    [Fact]
    public async Task Recalculate_SumsActiveShipmentLinesAndPreservesManualFinalValues()
    {
        await using var db = TestDbFactory.Create();
        var shipment = new Shipment { No = "SHP-1", FinalTotalCbm = 9m, FinalGrossWeightKg = 8m, FinalNetWeightKg = 7m };
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();
        db.DocumentLines.AddRange(
            new DocumentLine { DocumentType = "SHP", DocumentId = shipment.Id, TotalCbm = 1.234m, TotalGwKg = 100.555m, TotalNwKg = 90.444m },
            new DocumentLine { DocumentType = "SHP", DocumentId = shipment.Id, TotalCbm = 2.345m, TotalGwKg = 50.555m, TotalNwKg = 40.444m });
        await db.SaveChangesAsync();

        var service = new ShipmentMeasurementService(db);
        var result = await service.RecalculateAsync(shipment.Id, false);

        Assert.Equal(3.58m, result.CalculatedTotalCbm);
        Assert.Equal(151.11m, result.CalculatedGrossWeightKg);
        Assert.Equal(130.89m, result.CalculatedNetWeightKg);
        Assert.Equal(9m, result.FinalTotalCbm);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```bash
cd api
dotnet test Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter ShipmentMeasurementServiceTests
```

Expected: FAIL，缺少服务。

- [ ] **Step 3: 实现服务**

```csharp
using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public record ShipmentMeasurementResult(decimal Cbm, decimal GrossWeightKg, decimal NetWeightKg);

public class ShipmentMeasurementService
{
    private readonly AppDbContext _db;
    public ShipmentMeasurementService(AppDbContext db) => _db = db;

    public async Task<ShipmentMeasurementResult> CalculateAsync(long shipmentId)
    {
        var lines = await _db.DocumentLines
            .Where(x => !x.IsDeleted && x.DocumentType == "SHP" && x.DocumentId == shipmentId)
            .ToListAsync();
        return new ShipmentMeasurementResult(
            FinanceBalanceService.Round2(lines.Sum(x => x.TotalCbm)),
            FinanceBalanceService.Round2(lines.Sum(x => x.TotalGwKg)),
            FinanceBalanceService.Round2(lines.Sum(x => x.TotalNwKg)));
    }

    public async Task<Shipment> RecalculateAsync(long shipmentId, bool overwriteFinalValues)
    {
        var shipment = await _db.Shipments.FindAsync(shipmentId)
            ?? throw new KeyNotFoundException("Shipment not found");
        var result = await CalculateAsync(shipmentId);
        shipment.CalculatedTotalCbm = result.Cbm;
        shipment.CalculatedGrossWeightKg = result.GrossWeightKg;
        shipment.CalculatedNetWeightKg = result.NetWeightKg;
        if (overwriteFinalValues || (shipment.FinalTotalCbm == 0m && shipment.FinalGrossWeightKg == 0m && shipment.FinalNetWeightKg == 0m))
        {
            shipment.FinalTotalCbm = result.Cbm;
            shipment.FinalGrossWeightKg = result.GrossWeightKg;
            shipment.FinalNetWeightKg = result.NetWeightKg;
        }
        shipment.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return shipment;
    }
}
```

- [ ] **Step 4: 注册服务并运行测试**

在 `Program.cs` 加入：

```csharp
builder.Services.AddScoped<ShipmentMeasurementService>();
```

Run:

```bash
dotnet test Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter ShipmentMeasurementServiceTests
```

Expected: PASS。

- [ ] **Step 5: 提交**

```bash
git add api/Futurem.Sourcing.Api/Services/ShipmentMeasurementService.cs api/Futurem.Sourcing.Api/Program.cs api/Futurem.Sourcing.Api.Tests/Services/ShipmentMeasurementServiceTests.cs
git commit -m "feat: calculate shipment volume and weights"
```

---

### Task 4: 实现出运费用初始化、唯一性和 CRUD

**Files:**
- Create: `api/Futurem.Sourcing.Api/Services/ShipmentExpenseService.cs`
- Create: `api/Futurem.Sourcing.Api/Controllers/ShipmentExpensesController.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/ShipmentExpenseServiceTests.cs`
- Modify: `api/Futurem.Sourcing.Api/Program.cs`

**Interfaces:**
- Produces: `NormalizeName(string)`、`EnsureDefaultsAsync(long)`、`ValidateAsync(Shipment, ShipmentExpense, long? excludeId)`、`RecalculateExpenseTotalAsync(long)`。
- API: `GET/POST/PUT/DELETE /api/shipments/{shipmentId}/expenses`。

- [ ] **Step 1: 写失败测试**

测试必须覆盖：默认四行、同名自定义费用拒绝、金额大于 0 未选供应商拒绝、币种继承出运单。

```csharp
[Fact]
public async Task EnsureDefaults_CreatesExactlyFourFixedExpenses()
{
    await using var db = TestDbFactory.Create();
    var shipment = new Shipment { No = "SHP-1", Currency = "USD" };
    db.Shipments.Add(shipment);
    await db.SaveChangesAsync();
    var service = new ShipmentExpenseService(db);

    await service.EnsureDefaultsAsync(shipment.Id);
    await service.EnsureDefaultsAsync(shipment.Id);

    var rows = db.ShipmentExpenses.Where(x => x.ShipmentId == shipment.Id).OrderBy(x => x.SortNo).ToList();
    Assert.Equal(4, rows.Count);
    Assert.All(rows, x => Assert.Equal("USD", x.Currency));
}
```

- [ ] **Step 2: 运行测试确认失败**

```bash
cd api
dotnet test Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter ShipmentExpenseServiceTests
```

Expected: FAIL。

- [ ] **Step 3: 实现 ShipmentExpenseService**

固定费用常量：

```csharp
private static readonly (string Code, string Name, int SortNo)[] Defaults =
[
    ("OCEAN_FREIGHT", "海运费", 10),
    ("WAREHOUSE_FEE", "仓库费", 20),
    ("HANDLING_FEE", "装卸费", 30),
    ("INLAND_FREIGHT", "内陆费", 40)
];
```

标准化方法：

```csharp
public static string NormalizeName(string value)
    => string.Join(' ', value.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
```

校验逻辑必须抛出明确异常：

```csharp
if (expense.Amount < 0m) throw new InvalidOperationException("费用金额不能小于 0");
if (expense.Amount > 0m && !expense.SupplierId.HasValue) throw new InvalidOperationException($"{expense.ExpenseName}金额大于 0，请选择供应商");
if (!string.Equals(expense.Currency, shipment.Currency, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("出运单币种与费用币种不一致");
```

自定义费用代码生成：

```csharp
expense.NormalizedExpenseName = NormalizeName(expense.ExpenseName);
expense.ExpenseCode = expense.IsCustom ? $"CUSTOM:{expense.NormalizedExpenseName}" : expense.ExpenseCode;
```

费用总额：

```csharp
shipment.ExpenseTotal = FinanceBalanceService.Round2(await _db.ShipmentExpenses
    .Where(x => x.ShipmentId == shipmentId && !x.IsDeleted)
    .SumAsync(x => x.Amount));
```

- [ ] **Step 4: 创建嵌套路由控制器**

控制器路由：

```csharp
[ApiController]
[Route("api/shipments/{shipmentId:long}/expenses")]
public class ShipmentExpensesController : ControllerBase
```

删除规则：固定费用只能金额归零；自定义费用存在 `FinanceRecordId` 且对应应付已结算时返回 400；未生成财务记录时软删除。

- [ ] **Step 5: 注册服务并运行测试**

```csharp
builder.Services.AddScoped<ShipmentExpenseService>();
```

```bash
dotnet test Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter ShipmentExpenseServiceTests
dotnet build Futurem.Sourcing.Api/Futurem.Sourcing.Api.csproj -c Release
```

Expected: PASS / Build succeeded。

- [ ] **Step 6: 提交**

```bash
git add api/Futurem.Sourcing.Api/Services/ShipmentExpenseService.cs api/Futurem.Sourcing.Api/Controllers/ShipmentExpensesController.cs api/Futurem.Sourcing.Api/Program.cs api/Futurem.Sourcing.Api.Tests/Services/ShipmentExpenseServiceTests.cs
git commit -m "feat: add shipment expense management"
```

---

### Task 5: 实现供应商预付款生成、释放和自动抵扣

**Files:**
- Create: `api/Futurem.Sourcing.Api/Services/SupplierPrepaymentService.cs`
- Create: `api/Futurem.Sourcing.Api/Controllers/SupplierPrepaymentsController.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/SupplierPrepaymentServiceTests.cs`
- Modify: `api/Futurem.Sourcing.Api/Program.cs`

**Interfaces:**
- Produces: `UpsertOverpaymentAsync(FinanceRecord, decimal desiredTransfer)`。
- Produces: `ReleaseApplicationsAsync(FinanceRecord, decimal maxCreditToKeep)`。
- Produces: `ApplyAvailableAsync(FinanceRecord)`。
- API: `GET /api/supplier-prepayments` 和 `GET /api/supplier-prepayments/{id}/usages`。

- [ ] **Step 1: 写失败测试**

必须覆盖：同供应商同币种 FIFO 抵扣、不同币种不抵扣、金额下降生成预付款、重复同步不重复生成。

```csharp
[Fact]
public async Task ApplyAvailable_UsesSameSupplierAndCurrencyOldestFirst()
{
    await using var db = TestDbFactory.Create();
    db.SupplierPrepayments.AddRange(
        new SupplierPrepayment { No = "ADV-1", SupplierId = 10, Currency = "USD", OriginalAmount = 300m, AvailableAmount = 300m, Status = "available", CreatedAt = new DateTime(2026, 1, 1) },
        new SupplierPrepayment { No = "ADV-2", SupplierId = 10, Currency = "USD", OriginalAmount = 500m, AvailableAmount = 500m, Status = "available", CreatedAt = new DateTime(2026, 2, 1) },
        new SupplierPrepayment { No = "ADV-3", SupplierId = 10, Currency = "RMB", OriginalAmount = 999m, AvailableAmount = 999m, Status = "available" });
    var payable = new FinanceRecord { No = "AP-1", RecordType = "payable", SupplierId = 10, Currency = "USD", Amount = 600m };
    db.FinanceRecords.Add(payable);
    await db.SaveChangesAsync();

    var service = new SupplierPrepaymentService(db);
    await service.ApplyAvailableAsync(payable);

    Assert.Equal(600m, payable.PrepaymentAppliedAmount);
    Assert.Equal(0m, db.SupplierPrepayments.Single(x => x.No == "ADV-1").AvailableAmount);
    Assert.Equal(200m, db.SupplierPrepayments.Single(x => x.No == "ADV-2").AvailableAmount);
    Assert.Equal(999m, db.SupplierPrepayments.Single(x => x.No == "ADV-3").AvailableAmount);
}
```

- [ ] **Step 2: 运行测试确认失败**

```bash
cd api
dotnet test Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter SupplierPrepaymentServiceTests
```

Expected: FAIL。

- [ ] **Step 3: 实现抵扣算法**

`ApplyAvailableAsync` 必须：

1. 调用 `FinanceBalanceService.Outstanding(record)`。
2. 查询 `SupplierId` 和 `Currency` 相同、状态为 `available` 或 `partially_used`、`AvailableAmount > 0` 的预付款。
3. 按 `CreatedAt`、`Id` 升序。
4. 每次取 `Math.Min(outstanding, prepayment.AvailableAmount)`。
5. 新增 `SupplierPrepaymentUsage`，`UsageType = "apply"`。
6. 更新 `AvailableAmount` 和状态。
7. 累加 `FinanceRecord.PrepaymentAppliedAmount`。
8. 调用 `FinanceBalanceService.RefreshStatus`。

- [ ] **Step 4: 实现释放算法**

`ReleaseApplicationsAsync` 读取当前财务记录的 `apply` 流水，按最新优先释放超过 `maxCreditToKeep` 的部分；释放时新增 `UsageType = "release"` 的负向/独立流水，并把预付款余额恢复。最终 `PrepaymentAppliedAmount` 必须等于保留值。

- [ ] **Step 5: 实现超付预付款幂等更新**

来源唯一条件：

```csharp
x.SourceType == "SHIPMENT_EXPENSE_OVERPAYMENT" && x.SourceFinanceRecordId == finance.Id
```

`desiredTransfer` 小于现有 `OriginalAmount` 时，只能减少未使用的 `AvailableAmount`；已经被其他应付使用的部分不得回收。实际可减少值为：

```csharp
var used = existing.OriginalAmount - existing.AvailableAmount;
var minimumOriginal = Math.Max(0m, used);
var newOriginal = Math.Max(minimumOriginal, desiredTransfer);
```

同步 `finance.OverpaymentTransferredAmount = newOriginal`。

- [ ] **Step 6: 创建查询控制器**

列表支持 `supplierId`、`currency`、`status`，返回预付款原始金额、可用余额和来源。流水接口按 `CreatedAt` 升序返回。

- [ ] **Step 7: 注册、测试和提交**

```csharp
builder.Services.AddScoped<SupplierPrepaymentService>();
```

```bash
dotnet test Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter SupplierPrepaymentServiceTests
git add api/Futurem.Sourcing.Api/Services/SupplierPrepaymentService.cs api/Futurem.Sourcing.Api/Controllers/SupplierPrepaymentsController.cs api/Futurem.Sourcing.Api/Program.cs api/Futurem.Sourcing.Api.Tests/Services/SupplierPrepaymentServiceTests.cs
git commit -m "feat: add supplier prepayment ledger"
```

---

### Task 6: 实现出运费用到应付的幂等同步

**Files:**
- Create: `api/Futurem.Sourcing.Api/Services/ShipmentFinanceSyncService.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/ShipmentFinanceSyncServiceTests.cs`
- Modify: `api/Futurem.Sourcing.Api/Program.cs`

**Interfaces:**
- Produces: `Task SyncAsync(long shipmentId)`。
- Consumes: `ShipmentExpenseService`、`SupplierPrepaymentService`、`FinanceBalanceService`。

- [ ] **Step 1: 写失败测试**

测试至少包含：

```csharp
[Fact]
public async Task Sync_CreatesOnePayablePerPositiveExpenseAndIsIdempotent()
{
    await using var db = TestDbFactory.Create();
    var shipment = new Shipment { No = "SHP-1", Currency = "USD", Status = "confirmed" };
    db.Shipments.Add(shipment);
    await db.SaveChangesAsync();
    db.ShipmentExpenses.AddRange(
        new ShipmentExpense { ShipmentId = shipment.Id, ExpenseCode = "OCEAN_FREIGHT", ExpenseName = "海运费", NormalizedExpenseName = "海运费", SupplierId = 1, Amount = 1000m, Currency = "USD" },
        new ShipmentExpense { ShipmentId = shipment.Id, ExpenseCode = "WAREHOUSE_FEE", ExpenseName = "仓库费", NormalizedExpenseName = "仓库费", SupplierId = 2, Amount = 300m, Currency = "USD" });
    await db.SaveChangesAsync();

    var service = BuildSyncService(db);
    await service.SyncAsync(shipment.Id);
    await service.SyncAsync(shipment.Id);

    Assert.Equal(2, db.FinanceRecords.Count(x => x.RecordType == "payable"));
}
```

另外添加测试：金额从 1000 降到 700、已付 800 时生成 100 预付款；供应商变更时旧应付转出、新供应商创建新应付。

- [ ] **Step 2: 运行测试确认失败**

```bash
cd api
dotnet test Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter ShipmentFinanceSyncServiceTests
```

Expected: FAIL。

- [ ] **Step 3: 实现同步主流程**

核心流程必须是：

```csharp
public async Task SyncAsync(long shipmentId)
{
    var shipment = await _db.Shipments.FindAsync(shipmentId)
        ?? throw new KeyNotFoundException("Shipment not found");
    if (shipment.Status is not ("confirmed" or "shipped" or "completed"))
        throw new InvalidOperationException("草稿出运单不能同步财务");

    var expenses = await _db.ShipmentExpenses
        .Where(x => x.ShipmentId == shipmentId && !x.IsDeleted)
        .OrderBy(x => x.SortNo)
        .ThenBy(x => x.Id)
        .ToListAsync();

    foreach (var expense in expenses)
        await SyncExpenseAsync(shipment, expense);

    shipment.ExpenseTotal = FinanceBalanceService.Round2(expenses.Sum(x => x.Amount));
    shipment.FinanceSyncStatus = "synced";
    shipment.FinanceSyncMessage = null;
    shipment.FinanceSyncedAt = DateTime.Now;
    await _db.SaveChangesAsync();
}
```

- [ ] **Step 4: 实现单条费用同步**

规则：

- `Amount == 0`：未付款应付可设为 0 并标记完成；有付款时走超付转预付款。
- 没有应付：创建 `RecordType="payable"`、`TargetType="SHIPMENT_EXPENSE"`、`TargetId=expense.Id`、`ShipmentExpenseId=expense.Id`、`SourceKey=$"SHIPMENT_EXPENSE:{expense.Id}"`。
- 供应商未变：更新金额并重新平衡预付款。
- 供应商变更：先释放旧应付的预付款抵扣；现金超付转原供应商预付款；旧应付保留为历史并解除 `ShipmentExpenseId`，新建当前供应商应付。
- 每次同步先释放超过新金额所需的预付款应用，再计算现金超付，再应用可用预付款。

金额重平衡顺序：

```text
释放多余预付款抵扣 → 计算现金超付转出 → 更新应付金额 → 自动应用新预付款 → 刷新状态
```

- [ ] **Step 5: 同步错误状态**

服务内部发生异常时不吞异常。调用方负责事务回滚，并将 `FinanceSyncStatus="error"`、`FinanceSyncMessage=ex.Message` 写入独立补偿保存流程。

- [ ] **Step 6: 注册、测试和提交**

```csharp
builder.Services.AddScoped<ShipmentFinanceSyncService>();
```

```bash
dotnet test Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter ShipmentFinanceSyncServiceTests
git add api/Futurem.Sourcing.Api/Services/ShipmentFinanceSyncService.cs api/Futurem.Sourcing.Api/Program.cs api/Futurem.Sourcing.Api.Tests/Services/ShipmentFinanceSyncServiceTests.cs
git commit -m "feat: sync shipment expenses to payables"
```

---

### Task 7: 增加出运单确认、已出运、重新计算与同步 API

**Files:**
- Modify: `api/Futurem.Sourcing.Api/Controllers/ShipmentsController.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Controllers/ShipmentsControllerTests.cs`

**Interfaces:**
- API: `POST /api/shipments/{id}/recalculate-measurements`
- API: `POST /api/shipments/{id}/confirm`
- API: `POST /api/shipments/{id}/mark-shipped`
- API: `POST /api/shipments/{id}/sync-finance`

- [ ] **Step 1: 改造控制器构造函数**

```csharp
private readonly ShipmentExpenseService _expenseService;
private readonly ShipmentMeasurementService _measurementService;
private readonly ShipmentFinanceSyncService _financeSyncService;

public ShipmentsController(
    AppDbContext db,
    ShipmentExpenseService expenseService,
    ShipmentMeasurementService measurementService,
    ShipmentFinanceSyncService financeSyncService)
{
    _db = db;
    _expenseService = expenseService;
    _measurementService = measurementService;
    _financeSyncService = financeSyncService;
}
```

- [ ] **Step 2: 创建出运单时初始化费用和最终值**

`Create`、`GenerateFromContainer`、`Copy` 保存 Shipment 后调用：

```csharp
await _expenseService.EnsureDefaultsAsync(shipment.Id);
await _measurementService.RecalculateAsync(shipment.Id, true);
```

复制出运单时不复制财务关联和付款，只复制费用名称、供应商、金额、币种和备注为草稿数据。

- [ ] **Step 3: 标准化 Update**

Update 只能接受状态 `draft|confirmed|shipped|completed|cancelled`；普通 Header 更新不得直接触发敏感状态迁移。状态变化必须走专用端点。更新币种前检查费用：如果存在已生成应付，禁止直接更换币种。

- [ ] **Step 4: 添加重新计算端点**

请求模型：

```csharp
public record RecalculateMeasurementsRequest(bool OverwriteFinalValues = false);
```

端点调用 `RecalculateAsync(id, request.OverwriteFinalValues)`。

- [ ] **Step 5: 添加确认和已出运事务端点**

两个端点使用相同私有方法：

```csharp
private async Task<ActionResult<Shipment>> ChangeStatusAndSync(long id, string targetStatus)
{
    await using var transaction = await _db.Database.BeginTransactionAsync();
    try
    {
        var shipment = await _db.Shipments.FindAsync(id);
        if (shipment == null) return NotFound();
        await _expenseService.ValidateAllAsync(id);
        await _measurementService.RecalculateAsync(id, false);
        shipment.Status = targetStatus;
        shipment.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        await _financeSyncService.SyncAsync(id);
        await transaction.CommitAsync();
        return shipment;
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        await MarkFinanceSyncErrorAsync(id, ex.Message);
        return BadRequest(new { message = ex.Message });
    }
}
```

InMemory 测试不支持事务时，控制器测试直接验证校验和状态；MySQL 事务由 Docker 验收测试覆盖。

- [ ] **Step 6: 添加手工同步端点**

仅允许 `confirmed|shipped|completed`；同样使用事务和错误状态。

- [ ] **Step 7: 写控制器测试并运行**

覆盖：未选供应商确认返回 400、确认后生成应付、重复确认不重复、草稿手工同步拒绝。

```bash
cd api
dotnet test Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter ShipmentsControllerTests
```

Expected: PASS。

- [ ] **Step 8: 提交**

```bash
git add api/Futurem.Sourcing.Api/Controllers/ShipmentsController.cs api/Futurem.Sourcing.Api.Tests/Controllers/ShipmentsControllerTests.cs
git commit -m "feat: add shipment lifecycle and finance sync endpoints"
```

---

### Task 8: 统一财务余额、付款状态与利润统计

**Files:**
- Modify: `api/Futurem.Sourcing.Api/Controllers/FinanceRecordsController.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/PaymentsController.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/Payment.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Controllers/FinanceRecordsControllerTests.cs`

**Interfaces:**
- All payable balances use `FinanceBalanceService.Outstanding`。
- Payment creation/deletion calls `FinanceBalanceService.RefreshStatus`。

- [ ] **Step 1: 调整 Payment 精度**

将 `Amount` 和 `FeeAmount` 改为 `decimal(18,2)`；汇率保持 `decimal(18,6)`。

- [ ] **Step 2: 改造 PaymentsController**

创建付款前：

```csharp
input.Amount = FinanceBalanceService.Round2(input.Amount);
input.FeeAmount = FinanceBalanceService.Round2(input.FeeAmount);
if (input.Amount <= 0m) return BadRequest("付款金额必须大于 0");
```

更新 `PaidAmount` 后调用：

```csharp
FinanceBalanceService.RefreshStatus(finance);
```

删除付款同样调用统一状态函数。

- [ ] **Step 3: 改造 FinanceRecordsController**

所有余额从：

```csharp
x.Amount - x.PaidAmount
```

改为：

```csharp
FinanceBalanceService.Outstanding(x)
```

`profit-summary` 中出运费用应计入费用：

```csharp
var shipmentExpense = records
    .Where(x => x.RecordType == "payable" && x.TargetType == "SHIPMENT_EXPENSE")
    .Sum(x => x.Amount);
var netProfit = soIncome + otherIncome - poCost - shipmentExpense - expense;
```

返回对象增加 `shipmentExpense`。

- [ ] **Step 4: 写测试**

测试 `Amount=1000, Paid=400, PrepaymentApplied=300, OverpaymentTransferred=0` 的应付余额为 300；利润统计减去出运费用。

- [ ] **Step 5: 运行测试和提交**

```bash
cd api
dotnet test Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter FinanceRecordsControllerTests
git add api/Futurem.Sourcing.Api/Controllers/FinanceRecordsController.cs api/Futurem.Sourcing.Api/Controllers/PaymentsController.cs api/Futurem.Sourcing.Api/Entities/Payment.cs api/Futurem.Sourcing.Api.Tests/Controllers/FinanceRecordsControllerTests.cs
git commit -m "fix: include prepayments in finance balances"
```

---

### Task 9: 商品条码可修改与两位小数精度

**Files:**
- Modify: `api/Futurem.Sourcing.Api/Controllers/ProductsController.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/Product.cs`
- Modify: `api/Futurem.Sourcing.Api/Entities/DocumentLine.cs`
- Modify: `api/Futurem.Sourcing.Api/Controllers/DocumentLinesController.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Controllers/ProductsControllerTests.cs`
- Create: `api/Futurem.Sourcing.Api.Tests/Services/DocumentLinePrecisionTests.cs`

**Interfaces:**
- Product update accepts `Barcode` and preserves `Sku` unchanged。
- Document calculations round amount/cbm/gw/nw to 2 decimals。

- [ ] **Step 1: 写条码修改失败测试**

```csharp
[Fact]
public async Task Update_AllowsUniqueBarcodeChangeAndRejectsDuplicateIncludingDeletedRows()
{
    await using var db = TestDbFactory.Create();
    var p1 = new Product { Sku = "SKU1", Barcode = "111", NameCn = "A" };
    var p2 = new Product { Sku = "SKU2", Barcode = "222", NameCn = "B", IsDeleted = true };
    db.Products.AddRange(p1, p2);
    await db.SaveChangesAsync();
    var controller = new ProductsController(db);

    var ok = await controller.Update(p1.Id, new Product { Barcode = "333", NameCn = "A", Unit = "PCS" });
    Assert.Equal("333", ok.Value!.Barcode);

    var bad = await controller.Update(p1.Id, new Product { Barcode = "222", NameCn = "A", Unit = "PCS" });
    Assert.IsType<BadRequestObjectResult>(bad.Result);
}
```

- [ ] **Step 2: 修改 ProductsController.Update**

在更新其他字段前执行：

```csharp
var barcode = input.Barcode?.Trim() ?? string.Empty;
if (string.IsNullOrWhiteSpace(barcode)) return BadRequest("Barcode required");
if (barcode.Length > 80) return BadRequest("Barcode length must be <= 80");
if (await _db.Products.IgnoreQueryFilters().AnyAsync(x => x.Id != id && x.Barcode == barcode))
    return BadRequest("Barcode already exists");
entity.Barcode = barcode;
```

不允许通过 Update 修改 SKU。

- [ ] **Step 3: 调整数值实体精度**

`Product` 的 `PurchasePrice`、`CartonQty`、尺寸、毛重、净重改为 `decimal(18,2)`。

`DocumentLine` 的 `Quantity`、`UnitPrice`、`Amount`、`CartonQty`、`Cartons`、尺寸、`CartonCbm`、`TotalCbm`、毛重、净重全部改为 `decimal(18,2)`。

- [ ] **Step 4: 修改 DocumentLinesController.Calculate**

```csharp
line.Amount = FinanceBalanceService.Round2(line.Quantity * line.UnitPrice);
line.CartonCbm = FinanceBalanceService.Round2(line.CartonLengthCm * line.CartonWidthCm * line.CartonHeightCm / 1000000m);
line.TotalCbm = FinanceBalanceService.Round2(line.CartonCbm * line.Cartons);
line.TotalGwKg = FinanceBalanceService.Round2(line.CartonGwKg * line.Cartons);
line.TotalNwKg = FinanceBalanceService.Round2(line.CartonNwKg * line.Cartons);
```

- [ ] **Step 5: 运行测试和提交**

```bash
cd api
dotnet test Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj --filter "ProductsControllerTests|DocumentLinePrecisionTests"
git add api/Futurem.Sourcing.Api/Controllers/ProductsController.cs api/Futurem.Sourcing.Api/Entities/Product.cs api/Futurem.Sourcing.Api/Entities/DocumentLine.cs api/Futurem.Sourcing.Api/Controllers/DocumentLinesController.cs api/Futurem.Sourcing.Api.Tests
git commit -m "feat: allow barcode updates and enforce two-decimal precision"
```

---

### Task 10: 增加前端纯函数与测试

**Files:**
- Create: `web/src/utils/shipmentFinance.ts`
- Create: `web/tests/shipmentFinance.test.ts`
- Modify: `web/package.json`

**Interfaces:**
- Produces: `round2`、`normalizeExpenseName`、`expenseOutstanding`、`shipmentStatusLabel`。

- [ ] **Step 1: 写前端失败测试**

```ts
import assert from 'node:assert/strict'
import { expenseOutstanding, normalizeExpenseName, round2, shipmentStatusLabel } from '../src/utils/shipmentFinance.ts'

assert.equal(round2(1.005), 1.01)
assert.equal(normalizeExpenseName('  ocean   freight  '), 'OCEAN FREIGHT')
assert.equal(expenseOutstanding({ amount: 1000, paidAmount: 400, prepaymentAppliedAmount: 300, overpaymentTransferredAmount: 0 }), 300)
assert.equal(shipmentStatusLabel('confirmed'), '已确认')
console.log('shipment finance calculations passed')
```

- [ ] **Step 2: 实现工具文件**

```ts
export function round2(value: number): number {
  return Math.round((Number(value || 0) + Number.EPSILON) * 100) / 100
}

export function normalizeExpenseName(value: string): string {
  return String(value || '').trim().toUpperCase().split(/\s+/).filter(Boolean).join(' ')
}

export function expenseOutstanding(row: any): number {
  return Math.max(0, round2(Number(row.amount || 0) - Number(row.paidAmount || 0) - Number(row.prepaymentAppliedAmount || 0) + Number(row.overpaymentTransferredAmount || 0)))
}

export function shipmentStatusLabel(status: string): string {
  return ({ draft: '草稿', confirmed: '已确认', shipped: '已出运', completed: '已完成', cancelled: '已取消' } as Record<string, string>)[status] || status
}
```

- [ ] **Step 3: 更新测试脚本**

`package.json`：

```json
"test": "node tests/documentLineCalc.test.ts && node tests/shipmentFinance.test.ts"
```

- [ ] **Step 4: 运行测试和提交**

```bash
cd web
npm test
npm run build
git add src/utils/shipmentFinance.ts tests/shipmentFinance.test.ts package.json package-lock.json
git commit -m "test: add shipment finance frontend utilities"
```

Expected: 两个测试输出通过，Vite build 成功。

---

### Task 11: 创建出运体积重量和费用编辑组件

**Files:**
- Create: `web/src/components/ShipmentMeasurements.vue`
- Create: `web/src/components/ShipmentExpensesEditor.vue`

**Interfaces:**
- `ShipmentMeasurements` props: `shipment:any`，emits `recalculate(overwrite:boolean)`。
- `ShipmentExpensesEditor` props: `shipmentId:number`、`currency:string`、`shipmentStatus:string`，emits `changed`。

- [ ] **Step 1: 实现 ShipmentMeasurements**

组件显示三组数据：计算立方/最终立方、计算毛重/最终毛重、计算净重/最终净重。计算值只读，最终值使用 `el-input-number :precision="2" :min="0"`。提供：

```vue
<el-button @click="$emit('recalculate', false)">重新计算</el-button>
<el-button @click="$emit('recalculate', true)">重新计算并覆盖最终值</el-button>
```

- [ ] **Step 2: 实现 ShipmentExpensesEditor 数据加载**

加载：

```ts
const expenses = ref<any[]>([])
const suppliers = ref<any[]>([])
async function load() {
  expenses.value = (await http.get(`/shipments/${props.shipmentId}/expenses`)).data
  suppliers.value = (await http.get('/suppliers')).data
}
```

- [ ] **Step 3: 实现费用表列**

列必须包含：费用名称、供应商、金额、已付、预付款抵扣、未付、应付状态、备注、操作。金额输入统一 `precision=2`。固定费用禁止修改名称和删除；自定义费用可删除但由后端决定是否允许。

- [ ] **Step 4: 实现新增自定义费用**

表单字段：费用名称、供应商、金额、备注。提交数据：

```ts
{
  expenseCode: '',
  expenseName: customForm.expenseName,
  isCustom: true,
  supplierId: customForm.supplierId,
  amount: round2(customForm.amount),
  currency: props.currency,
  remark: customForm.remark
}
```

- [ ] **Step 5: 实现保存与错误提示**

后端错误优先显示：

```ts
catch (error: any) {
  ElMessage.error(error?.response?.data?.message || error?.response?.data || '保存失败')
}
```

保存成功后刷新并 emit `changed`。

- [ ] **Step 6: 编译和提交**

```bash
cd web
npm run build
git add src/components/ShipmentMeasurements.vue src/components/ShipmentExpensesEditor.vue
git commit -m "feat: add shipment measurement and expense editors"
```

---

### Task 12: 整合出运单页面和财务页面

**Files:**
- Modify: `web/src/views/Shipments.vue`
- Modify: `web/src/views/FinanceRecords.vue`

**Interfaces:**
- Shipments view consumes Task 11 components and Task 7 endpoints。
- Finance view displays `prepaymentAppliedAmount` and `overpaymentTransferredAmount`。

- [ ] **Step 1: 修改 Shipments.vue 表单模型**

`form` 增加：

```ts
currency:'RMB',
calculatedTotalCbm:0,
finalTotalCbm:0,
calculatedGrossWeightKg:0,
finalGrossWeightKg:0,
calculatedNetWeightKg:0,
finalNetWeightKg:0,
expenseTotal:0,
financeSyncStatus:'not_synced',
financeSyncMessage:''
```

- [ ] **Step 2: 状态改为固定下拉**

普通表单中状态只读显示；底部通过专用按钮迁移状态。状态标签使用 `shipmentStatusLabel`。

- [ ] **Step 3: 插入测量与费用组件**

在 Header 后、DocumentLinesEditor 后分别插入：

```vue
<ShipmentMeasurements v-if="form.id" :shipment="form" @recalculate="recalculateMeasurements" />
<DocumentLinesEditor v-if="form.id" document-type="SHP" :document-id="form.id" />
<ShipmentExpensesEditor v-if="form.id" :shipment-id="form.id" :currency="form.currency" :shipment-status="form.status" @changed="reloadCurrent" />
```

- [ ] **Step 4: 增加操作方法**

```ts
async function confirmShipment(){ await http.post(`/shipments/${form.id}/confirm`); await reloadCurrent(); ElMessage.success('出运单已确认') }
async function markShipped(){ await http.post(`/shipments/${form.id}/mark-shipped`); await reloadCurrent(); ElMessage.success('已标记出运') }
async function syncFinance(){ await http.post(`/shipments/${form.id}/sync-finance`); await reloadCurrent(); ElMessage.success('财务同步完成') }
async function recalculateMeasurements(overwriteFinalValues:boolean){ const res=await http.post(`/shipments/${form.id}/recalculate-measurements`,{overwriteFinalValues}); Object.assign(form,res.data) }
async function reloadCurrent(){ const res=await http.get(`/shipments/${form.id}`); Object.assign(form,res.data); await load() }
```

- [ ] **Step 5: 更新 FinanceRecords.vue**

列表增加：

- `prepaymentAppliedAmount` 列：预付款抵扣。
- `overpaymentTransferredAmount` 列：转预付款。
- 未付使用 `expenseOutstanding(scope.row)`。
- `targetType === 'SHIPMENT_EXPENSE'` 显示“出运费用”。

- [ ] **Step 6: 前端完整验证和提交**

```bash
cd web
npm test
npm run build
git add src/views/Shipments.vue src/views/FinanceRecords.vue
git commit -m "feat: integrate shipment expenses and finance status UI"
```

---

### Task 13: 更新检查脚本、执行 MySQL/Docker 集成验收

**Files:**
- Modify: `scripts/check.sh`
- Modify: `README.md`
- Modify: `docs/03-BusinessFlow.md`

**Interfaces:**
- Produces a reproducible local validation command: `./scripts/check.sh`。

- [ ] **Step 1: 更新 check.sh**

API 部分改为：

```bash
dotnet restore api/Futurem.Sourcing.Api/Futurem.Sourcing.Api.csproj
dotnet build api/Futurem.Sourcing.Api/Futurem.Sourcing.Api.csproj -c Release
dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj -c Release
```

Web 部分改为：

```bash
cd web
npm ci
npm test
npm run build
```

- [ ] **Step 2: 运行静态检查**

```bash
chmod +x scripts/check.sh
./scripts/check.sh
```

Expected:

```text
Build succeeded.
Passed!
document line calculations passed
shipment finance calculations passed
Check completed
```

- [ ] **Step 3: 重新构建 Docker**

```bash
docker compose down
docker compose up -d --build
docker compose ps
```

Expected: `futurem_mysql` 和 `futurem_redis` healthy，`futurem_api`、`futurem_web` Up。

- [ ] **Step 4: 验证数据库升级**

```bash
docker compose logs api --tail=200
docker exec futurem_mysql mysql -uroot -pfuturem123456 futurem_sourcing -e "SHOW TABLES LIKE 'shipment_expenses'; SHOW TABLES LIKE 'supplier_prepayments'; SHOW TABLES LIKE 'supplier_prepayment_usages'; DESCRIBE shipments; DESCRIBE finance_records;"
```

Expected: 三张新表存在，Shipment 和 FinanceRecord 新列存在，API 日志没有升级异常。

- [ ] **Step 5: API 烟雾测试**

先创建或选择已有出运单 ID，然后执行：

```bash
curl -s http://localhost:8080/api/shipments/1/expenses
curl -s -X POST http://localhost:8080/api/shipments/1/recalculate-measurements -H 'Content-Type: application/json' -d '{"overwriteFinalValues":false}'
curl -s -X POST http://localhost:8080/api/shipments/1/confirm
curl -s 'http://localhost:8080/api/finance-records?targetType=SHIPMENT_EXPENSE'
```

Expected: 默认四个费用存在；未配置正金额费用时确认成功；正金额未选供应商时返回明确 400；配置供应商后确认生成对应应付。

- [ ] **Step 6: 手工验收关键财务场景**

1. 海运费 1000 USD，供应商 A，确认后生成一条 1000 应付。
2. 支付 800。
3. 把海运费改为 700。
4. 验证原应付金额 700、转出预付款 100、供应商 A 可用预付款 100。
5. 新建供应商 A、USD 的另一个出运费用应付 300。
6. 验证自动抵扣 100，未付 200。
7. 重复点击同步，验证没有重复应付或重复抵扣流水。

- [ ] **Step 7: 更新文档**

README 增加出运费用和预付款功能说明；业务流程增加：

```text
Shipment 出运单
  → 录入出运费用
  → 确认/已出运
  → 自动生成供应商应付
  → 自动抵扣供应商预付款
  → 付款与余额跟踪
```

- [ ] **Step 8: 最终提交**

```bash
git add scripts/check.sh README.md docs/03-BusinessFlow.md
git commit -m "docs: document shipment expense finance workflow"
```

- [ ] **Step 9: 最终全量验证**

```bash
./scripts/check.sh
docker compose up -d --build
docker compose ps
git status --short
```

Expected: 所有检查通过，四个服务正常，`git status --short` 无未提交文件。

---

## 计划自检结果

- 规格覆盖：固定费用、自定义费用、单币种、供应商必填、确认时生成、确认后修改、超付转预付款、自动抵扣、体积重量汇总、两位小数、条码修改、事务回滚、幂等、防重复和 Docker 验收均有对应任务。
- 占位符扫描：计划不包含 `TBD`、`TODO` 或“稍后实现”。
- 类型一致性：`ShipmentExpenseId`、`PrepaymentAppliedAmount`、`OverpaymentTransferredAmount`、`SourceKey` 在实体、服务、控制器和前端中命名一致。
- 范围控制：未加入跨币种折算、同类费用多行、总账凭证和银行对账。
