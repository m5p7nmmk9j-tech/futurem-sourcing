# 订单商品、客户汇总、仓库装柜与财务联动设计

日期：2026-07-11  
项目：FUTUREM Enterprise Sourcing ERP  
状态：用户已确认  
适用分支：`design/order-product-warehouse-flow-20260711`

## 1. 设计目标

本次改造把现有以固定商品档案和通用 `DocumentLine` 为中心的流程，升级为以“订单商品快照”为核心、以仓库实际库存为装柜来源、以实际接受数量和实际装柜数量为财务触发点的完整外贸采购业务链。

最终流程：

```text
客户订单 CO
→ 人工选择订单商品生成采购订单 PO
→ 客户汇总单
→ 供应商送货通知
→ 收货单
→ 验货单
→ 仓库库存
→ 装柜单
→ 出运单
→ 客户应收 / 供应商应付 / 收付款冲销
```

本设计同时覆盖：

1. 客户历史订单商品和每单独立商品资料。
2. 客户进口商资料、商品标签和外箱唛头模板。
3. 同客户多 PO、多供应商商品汇总和整箱数量预留。
4. 送货通知、分批收货、一次收货一次验货。
5. 验货后实际接受数量入库并生成供应商应付。
6. 仓库库存、库位、批次和业务来源追溯。
7. 装柜草稿库存锁定、三天自动释放和实际装柜出库。
8. 装柜后自动生成客户商品货款应收和出运单草稿。
9. 出运服务费用的物流供应商应付与客户费用应收。
10. 客户统一应收、供应商应付、预收预付和先进先出冲销。
11. 商品供应商与物流服务商彻底分离。
12. 市场菜单移除，系统所有金额统一使用人民币。

## 2. 已确认的全局规则

### 2.1 人民币单币种

- 所有采购、销售、仓储、装柜、出运、应收、应付、收款、付款均使用人民币。
- 前端不显示币种选择器，不提供汇率和汇兑损益功能。
- 所有金额统一显示 `¥`，数据库金额使用 `decimal(18,2)`。
- 为兼容已有数据表，现有 `Currency` 字段可暂时保留为技术兼容字段，但写入值必须固定为 `RMB`，API 不允许客户端提交其他值。
- 后续新表不新增可选币种字段。

### 2.2 数据不可覆盖原则

- 已确认单据的业务快照不得直接覆盖。
- 需要修改时，必须通过退回草稿、解锁、调整单或反冲单完成。
- 已发生的付款、收款、财务抵扣和审计日志不得删除。
- 所有数量和金额变更必须保留修改前、修改后、原因、操作人和时间。

### 2.3 数量精度

- 商品件数、箱数和箱规允许 `decimal(18,2)`，但汇总单分配必须按整箱进行。
- 金额、体积、毛重、净重统一保留两位小数。
- 财务计算使用后端 `decimal`，禁止使用浮点数。

### 2.4 并发原则

以下操作必须使用数据库事务和并发校验：

- 汇总单预留 PO 剩余箱数。
- 收货和验货数量确认。
- 仓库库存入库、锁定、释放、出库。
- 应收、应付、预收、预付自动抵扣。
- 装柜确认和出运确认。

任何累计数量不得超过来源单据或库存可用数量。

## 3. 领域边界

系统拆分为六个业务域：

1. **订单商品域**：客户订单、订单商品、历史复制、进口商、标签、唛头。
2. **采购汇总域**：采购订单、客户汇总单、整箱预留、送货通知。
3. **收货验货域**：分批收货、验货、实际接受数量、质量调整。
4. **仓库装柜域**：仓库、库位、库存批次、装柜锁定、实际出库。
5. **出运物流域**：一柜一出运单、物流服务、费用、服务商。
6. **财务结算域**：客户统一应收、供应商应付、收付款、预收预付、调整和反冲。

各域通过不可变业务来源 ID 连接，禁止仅依赖文本单号关联。

## 4. 订单商品设计

### 4.1 核心定义

“订单商品”不是固定商品主档，而是某次客户订单中的独立业务快照。同一个现实商品在不同订单中可以有不同：

- 图片
- 客户货号
- 客户条码
- 采购价格
- 客户销售价格
- 包装方式
- 箱规、尺寸、重量
- 商品供应商
- 标签和唛头模板
- 进口商资料

重复下单时复制历史订单商品生成新记录，历史记录保持不变。

### 4.2 `order_products`

新增表，建议字段：

- `Id`
- `CustomerId`
- `SupplierId`
- `SourceOrderProductId`
- `SourceCustomerOrderId`
- `SystemSku`
- `CustomerItemNo`
- `CustomerBarcode`
- `SupplierItemNo`
- `NameCn`
- `NameEn`
- `NameEs`
- `Specification`
- `Color`
- `Unit`
- `PurchaseUnitPrice`
- `SalesUnitPrice`
- `CartonQty`
- `CartonLengthCm`
- `CartonWidthCm`
- `CartonHeightCm`
- `CartonCbm`
- `CartonGwKg`
- `CartonNwKg`
- `ImporterProfileId`
- `ImporterSnapshotJson`
- `LabelTemplateId`
- `LabelTemplateSnapshotJson`
- `MarkTemplateId`
- `MarkTemplateSnapshotJson`
- `BatchCode`
- `Status`
- `LockedAt`
- 基础审计字段

约束：

- `CustomerBarcode` 在同一客户内必须唯一；为空时允许使用系统 SKU 生成条码。
- 客户商品编号显示优先级：客户货号 → 系统 SKU。
- 确认 CO 后，价格、包装、进口商、标签、唛头和条码形成快照并锁定。
- 新订单复制历史商品时，新记录的 `SourceOrderProductId` 指向来源记录。

### 4.3 `order_product_images`

字段：

- `Id`
- `OrderProductId`
- `ImageUrl`
- `ImageType`：`main`、`detail`、`package`、`reference`
- `SortNo`
- `FileName`
- `ContentType`
- `CreatedAt`

每个订单商品允许多张图片，主图最多一张。

### 4.4 `DocumentLine` 调整

现有 `document_lines` 继续作为各业务单据的数量和金额明细，但增加：

- `OrderProductId`
- `SourceDocumentLineId`
- `CustomerId`
- `SupplierId`
- `PurchaseUnitPriceSnapshot`
- `SalesUnitPriceSnapshot`
- `WarehouseId`
- `WarehouseLocationId`
- `InventoryLotId`

规则：

- 同一个 `OrderProductId` 可贯穿 CO、PO、汇总、送货、收货、验货、库存、装柜和出运。
- 每张单据仍保存自己的数量、箱数和计算金额。
- 已确认单据不得从当前商品资料重新读取价格，必须使用快照。

### 4.5 原商品菜单

- 主菜单“Products”改名为“客户历史商品”。
- 保留现有 `products` 表作为系统参考商品和兼容数据，不再作为新业务的唯一来源。
- 客户历史商品页面默认查询 `order_products`，可按客户、供应商、客户货号、条码、商品名称和历史订单筛选。
- 从历史商品创建新订单时必须复制，而不是复用原记录。

## 5. 客户、进口商和模板

### 5.1 `customer_importer_profiles`

一个客户可维护多个进口商资料：

- `Id`
- `CustomerId`
- `Name`
- `CompanyName`
- `TaxIdOrRfc`
- `Address`
- `ContactName`
- `Phone`
- `Email`
- `LogoUrl`
- `DefaultOriginText`
- `DefaultLabelTemplateId`
- `DefaultMarkTemplateId`
- `IsDefault`
- `Status`
- 审计字段

同一客户只能有一个有效默认进口商。

### 5.2 标签与唛头模板

扩展现有 `PrintTemplate`，或新增专用模板表。模板必须支持：

- `TemplateType`：`product_label`、`carton_mark`
- `CustomerId`
- `ImporterProfileId` 可空
- `DesignerMode`：`fixed`、`visual`
- `PaperWidthMm`
- `PaperHeightMm`
- `Orientation`
- `LayoutJson`
- `Body`
- `IsDefault`
- `Status`

同一客户可有多个标签模板和多个唛头模板。

支持变量：

- 客户 Logo
- 进口商公司、RFC、地址和联系方式
- 客户货号
- 系统 SKU
- 客户条码
- 商品名称和型号
- 数量和箱规
- 箱号
- 箱尺寸、毛重、净重
- 批次
- `Made in China`

批次默认格式：`YYYYMMDD`。

### 5.3 模板选择与锁定

- CO 创建时选择进口商资料。
- 系统自动带出该进口商默认标签和唛头模板。
- 草稿阶段可切换同一客户的其他模板。
- 直接创建 PO 时客户和进口商资料必填。
- CO 或直接 PO 确认后，模板 ID 和模板内容快照锁定。
- 标签和唛头业务内容不能在打印页面手工改写；必须回到草稿修改来源资料后重新生成。

### 5.4 供应商资料包

每个 PO 或送货通知可生成 ZIP：

- 商品标签 PDF
- 外箱唛头 PDF
- 打印数量 Excel

建议打印数量：

- 商品标签：商品件数
- 外箱唛头：箱数

允许在生成资料包前调整打印份数，但不能修改内容。

记录：

- 发布时间
- 发布人
- 供应商首次查看时间
- 每次下载时间
- 下载次数
- 文件版本和快照哈希

## 6. 商品供应商与物流服务商

### 6.1 商品供应商

现有 `Supplier` 作为商品供应商：

- 删除业务上的 `MarketId` 关联。
- 保留 `ShopNo`、`FloorNo`、`BoothNo`、主营产品和联系人信息。
- 市场页面和菜单移除。

### 6.2 `logistics_providers`

物流服务商独立建表，不与商品供应商共用主数据。

字段：

- `Id`
- `Code`
- `Name`
- `ServiceTypesJson`
- `ContactName`
- `Phone`
- `Email`
- `Address`
- `TaxId`
- `BankInfoJson`
- `Status`
- 审计字段

服务类型：

- 国际货代
- 报关行
- 拖车公司
- 仓库服务
- 快递公司
- 其他服务

同一家服务商可以具有多个服务类型。

## 7. 客户订单 CO

### 7.1 创建

- 先选择客户和进口商资料。
- 直接在订单中新增订单商品，或复制客户历史商品。
- 每条商品独立选择商品供应商、采购价、销售价、图片、包装、标签和唛头资料。
- 所有金额固定人民币。

### 7.2 状态

- `draft`：可编辑。
- `confirmed`：锁定全部业务快照。
- `partially_converted`：部分商品已生成 PO。
- `converted`：全部商品已生成 PO。
- `cancelled`：已取消。

确认后修改必须先退回草稿，并记录原因和审批。

### 7.3 生成 PO

- 采购人员手工勾选订单商品和数量生成 PO。
- 不自动按供应商拆单。
- 一张 PO 只能属于一个商品供应商。
- 一条订单商品在同一时点只能分配给一个商品供应商，但允许按数量分批生成多个 PO 时必须累计校验不超 CO 数量。
- PO 取消后，未执行数量恢复为待采购。

## 8. 采购订单 PO

### 8.1 基本规则

- 客户必填。
- 商品供应商必填。
- 进口商和标签/唛头快照从订单商品继承。
- PO 确认不生成应付。
- `PayStatus` 由财务模块计算，不允许业务人员直接修改。

### 8.2 可汇总数量

每条 PO 明细显示：

- PO 总件数、总箱数
- 草稿汇总预留件数、箱数
- 已确认汇总件数、箱数
- 已收货件数、箱数
- 验货接受件数、箱数
- 剩余可汇总件数、箱数

累计有效预留不得超过 PO 总箱数。

## 9. 客户汇总单

### 9.1 定义

原“销售订单 SO”更名为“客户汇总单”。它用于在收货前，把同一客户来自不同 PO、不同商品供应商的商品组合成计划柜，查看体积、重量和金额。

### 9.2 `summary_order_items`

新增关系表：

- `Id`
- `SummaryOrderId`
- `PurchaseOrderId`
- `PurchaseOrderLineId`
- `OrderProductId`
- `SupplierId`
- `ReservedCartons`
- `ReservedQuantity`
- `ReservationStatus`
- `ConfirmedAt`
- `ReleasedAt`
- `ReleaseReason`
- 审计字段

允许一条 PO 明细按整箱拆分到多张汇总单。

### 9.3 草稿预留

- 同一客户允许同时存在多张草稿汇总单。
- 商品加入草稿即预留箱数。
- 预留后，其他汇总单只能看到剩余可用箱数。
- 从草稿删除、取消汇总单或预留过期时释放。
- 使用事务和行级并发校验，防止两个用户超量预留。

### 9.4 汇总信息

汇总单实时显示：

- 商品数
- 供应商数
- 件数和箱数
- 总体积、毛重、净重
- 采购金额
- 客户销售金额
- 预计商品毛利
- 计划柜型
- 柜体积和载重利用率
- 剩余体积和剩余载重
- 超载、超体积和资料缺失警告

### 9.5 确认与追加

确认后：

- 锁定已有预留。
- 自动生成供应商送货通知计划。
- 允许在装柜单确认前追加同一客户的替补商品。
- 追加必须审批并记录原因。
- 原已确认商品不能直接删除。
- 装柜单确认后，参与该柜的汇总单统一标记 `loaded`，不再可用于后续装柜。

## 10. 供应商送货通知

### 10.1 数据模型

新增：

- `delivery_notices`
- `delivery_notice_lines`

通知头：

- 汇总单
- 商品供应商
- 计划送货日期
- 收货仓库
- 状态
- 供应商确认时间
- 发布时间

通知行：

- PO、PO 行、订单商品
- 计划箱数和件数
- 已通知箱数和件数
- 已收货箱数和件数
- 标签和唛头快照引用

### 10.2 合并规则

同一张通知可合并多个 PO 和商品，但必须同时满足：

- 同一商品供应商
- 同一汇总单
- 同一计划送货日期
- 同一收货仓库

一张通知只能送到一个仓库。同一供应商和同一汇总单允许多张通知。

### 10.3 数量规则

- 每张通知是一次计划送货，不是长期累积通知。
- 同一汇总商品的累计有效通知数量不得超过其确认预留数量。
- 通知只计划送货，不生成应付。

## 11. 收货与验货

### 11.1 收货单

一张送货通知可分多次收货，每次生成独立收货单。

收货单增加：

- `DeliveryNoticeId`
- `WarehouseId`
- `ActualArrivalAt`
- `ReceiverUserId`
- `TemporaryCountStatus`

收货行记录：

- 实际到货件数、箱数
- 外箱状态
- 临时计数
- 暂存库位

仓库初次点数仅为临时数量，不生成供应商应付。

### 11.2 一收货一验货

固定关系：

```text
1 张收货单 = 1 张验货单
```

- 一张验货单必须覆盖该收货单的全部实际到货数量。
- 若验货跨多天或需要分批验货，必须拆成多张收货单。
- `ReceivingOrderId` 在有效验货单中唯一。

### 11.3 验货行

新增 `qc_order_lines`，每行记录：

- 收货行
- PO 行
- 订单商品
- 到货件数和箱数
- 合格数量
- 不合格数量
- 退回数量
- 待处理数量
- 最终实际接受数量
- 缺少数量
- 质量问题和图片

数量恒等式：

```text
到货数量 = 合格 + 不合格 + 退回 + 待处理
最终实际接受数量 <= 到货数量
```

本项目确认采用：

- 不合格、退回、待处理数量不计入实际接受数量。
- 供应商应付只按最终实际接受数量生成。

### 11.4 验货确认

验货确认事务内完成：

1. 校验所有数量。
2. 确认最终实际接受数量。
3. 创建或增加仓库库存批次。
4. 释放汇总计划与实际接受数量的差额，使其恢复为 PO 可汇总数量。
5. 生成商品供应商应付。
6. 更新收货单、送货通知、PO 和汇总统计。
7. 写入审计日志。

应付公式：

```text
供应商应付 = 最终实际接受数量 × PO 采购单价快照
```

### 11.5 验货解锁和财务调整

- 仅有 `qc.unlock` 权限的用户可解锁。
- 解锁原因必填。
- 同一收货单仍只保留一张验货单，直接修改原验货单。
- 修改前后数量必须记录快照。
- 未付款应付自动重算。
- 已付款后实际接受数量减少：保留原付款，生成供应商退款/应付冲减调整单。
- 实际接受数量增加：生成补充应付。
- 供应商退款可形成现金退款或抵扣该供应商后续应付。

## 12. 仓库与库存

### 12.1 新增主数据

新增：

- `warehouses`
- `warehouse_locations`

仓库字段：代码、名称、地址、状态。  
库位字段：仓库、代码、区域、巷道、货架、层、状态。

一张送货通知、收货单和装柜单只能对应一个仓库。

### 12.2 `inventory_lots`

每次验货确认的实际接受数量形成库存批次：

- `Id`
- `WarehouseId`
- `WarehouseLocationId`
- `CustomerId`
- `OrderProductId`
- `PurchaseOrderId`
- `PurchaseOrderLineId`
- `DeliveryNoticeId`
- `ReceivingOrderId`
- `QcOrderId`
- `SummaryOrderId`
- `SupplierId`
- `BatchCode`
- `ReceivedQuantity`
- `ReceivedCartons`
- `OnHandQuantity`
- `OnHandCartons`
- `LockedQuantity`
- `LockedCartons`
- `AvailableQuantity`
- `AvailableCartons`
- 包装、尺寸和重量快照
- `Status`
- 审计字段

计算：

```text
可用数量 = 在库数量 - 有效锁定数量
可用箱数 = 在库箱数 - 有效锁定箱数
```

### 12.3 `inventory_transactions`

所有库存变化写流水：

- `receive`
- `qc_adjust`
- `load_lock`
- `load_unlock`
- `container_out`
- `transfer_out`
- `transfer_in`
- `manual_adjustment`
- `return`

流水不得删除或覆盖。

## 13. 装柜单

### 13.1 创建规则

创建装柜单时必须先选择：

- 一个客户
- 一个仓库

一张装柜单只能包含同一客户、同一仓库的库存。

可合并：

- 多个 PO
- 多个商品供应商
- 多张收货单
- 多个验货批次
- 多张原汇总单来源

装柜单直接从仓库库存选择商品，不要求先创建新的汇总单。

### 13.2 装柜明细

装柜行必须记录：

- `InventoryLotId`
- `OrderProductId`
- `CustomerId`
- `WarehouseId`
- 来源 PO、收货单、验货单和原汇总单
- 计划装柜件数和箱数
- 实际装柜件数和箱数
- 销售单价快照
- 采购单价快照
- 体积、毛重和净重

### 13.3 草稿库存锁定

商品加入装柜草稿后立即锁定。

- 锁定有效期为成功锁定后的 72 小时。
- 普通保存草稿不延长时间。
- 新增加的锁定项单独计算 72 小时。
- 删除明细、取消或作废装柜单立即释放。
- 到期时只释放库存，草稿保留并显示“库存锁定已过期”。
- 过期后重新打开必须重新锁定。
- 若库存被其他业务占用，重新锁定返回库存不足明细。

建议新增：

- `inventory_reservations`
- `ContainerLoadLine.InventoryReservationId`
- 后台定时任务 `ContainerReservationExpiryJob`

### 13.4 装柜确认

装柜确认事务内完成：

1. 验证所有库存锁定有效且属于同一客户、同一仓库。
2. 保存实际装柜数量。
3. 扣减库存批次在库数量并关闭锁定。
4. 写出库流水。
5. 按实际装柜数量生成客户商品货款应收。
6. 自动生成一张出运单草稿。
7. 将本柜涉及的原汇总单状态改为 `loaded`。
8. 写审计日志。

商品应收公式：

```text
商品应收 = 实际装柜数量 × 订单商品锁定销售单价
```

### 13.5 未装走商品

- 原汇总单不修改明细，也不自动生成剩余汇总单。
- 原汇总单参与装柜后显示“已装柜”，不能再次被选择。
- 未装走的商品继续留在仓库库存中。
- 后续新收货单验货入库后，与原留仓库存一起直接创建新的装柜单。
- 新装柜单通过库存批次保留原汇总单、PO、供应商、收货单和批次来源。
- 只有实际装走数量生成客户商品应收。

## 14. 出运单

### 14.1 一柜一出运单

固定关系：

```text
1 张装柜单 = 1 张出运单
```

- 装柜确认后自动生成出运单草稿。
- `ContainerLoadId` 在有效出运单中唯一。
- 不允许多个装柜单合并成一张出运单。

### 14.2 自动继承

- 客户
- 进口商
- 仓库
- 柜型
- 柜号
- 封条号
- 实际装柜明细
- 箱数、体积、毛重和净重
- 起运港和目的港默认值

草稿补充：船公司、货代、船名航次、提单号、ETD、ETA 和物流费用。

### 14.3 确认时点

货柜实际离开仓库、操作人员点击“确认发运”时：

- 出运单状态改为 `shipped`。
- 物流服务费用锁定。
- 为每条物流服务生成服务商应付。
- 将客户物流费用追加到本柜对应的客户统一应收单。
- 后续费用修改只能通过调整单。

## 15. 出运服务费用

### 15.1 费用行

将现有 `ShipmentExpense` 扩展为：

- `LogisticsProviderId`
- `ServiceType`
- `ProviderCost`
- `CustomerCharge`
- `ProviderPayableFinanceRecordId`
- `CustomerReceivableLineId`
- `ProfitAmount`
- `Status`
- 审计字段

同一张出运单可有多条服务，每条独立选择物流服务商。

### 15.2 费用规则

- 服务商成本和客户收费均为人民币。
- 同一服务类型允许多行，但同一出运单中 `服务类型 + 服务商 + 业务备注键` 不得重复。
- 出运确认后，为不同服务商分别生成应付。
- 客户收费追加到同一张客户应收单。

```text
物流利润 = 客户收费 - 服务商成本
```

## 16. 客户统一应收

### 16.1 应收头和明细

建议将 `FinanceRecord` 作为应收/应付头，新增 `finance_record_lines`。

客户应收头按装柜单唯一：

- 装柜确认时创建应收头。
- 加入商品货款明细。
- 出运确认时在同一应收头追加物流费用明细。

明细类型：

- `goods`
- `ocean_freight`
- `customs`
- `trucking`
- `warehouse`
- `courier`
- `other_service`
- `adjustment`

即使商品货款已部分或全部收款，仍允许追加物流费用；原收款记录保持不变，新增费用增加未收余额。

### 16.2 客户收款

- 按应收明细生成时间先进先出自动冲销。
- 一笔收款可冲销多条明细。
- 不足时最早明细为部分收款。
- 多收金额形成客户预收款。
- 客户预收款自动抵扣最早生成的后续应收。
- 错误收款通过反冲单处理。

建议新增：

- `customer_advances`
- `customer_advance_usages`
- `payment_allocations`

## 17. 供应商应付

### 17.1 商品供应商应付

验货确认时按每个商品供应商、PO 和实际接受行生成应付明细。

### 17.2 物流服务商应付

出运确认时按每个物流费用行生成应付明细。

商品供应商与物流服务商分开核算，不能相互冲销。

### 17.3 付款和预付款

- 按应付明细生成时间先进先出自动冲销。
- 一笔付款可冲销多条应付。
- 多付形成供应商预付款。
- 预付款自动抵扣最早生成的后续应付。
- 退款、冲减和错误付款使用调整单或反冲单。

现有 `SupplierPrepayment` 可继续使用，但固定 `Currency = RMB`，并增加 `CounterpartyType` 区分商品供应商和物流服务商。

## 18. 状态模型

### 18.1 汇总单

```text
draft
→ confirmed
→ receiving
→ qc_in_progress
→ ready_to_load
→ loaded
→ completed
```

异常状态：`cancelled`。

### 18.2 送货通知

```text
draft → published → supplier_confirmed → partially_received → received → closed
```

### 18.3 收货单

```text
draft → received → qc_in_progress → qc_confirmed → closed
```

### 18.4 验货单

```text
draft → confirmed → unlocked → confirmed
```

每次解锁和重新确认产生版本日志。

### 18.5 装柜单

```text
draft → inventory_locked → confirmed → shipment_created → completed
```

异常状态：`lock_expired`、`cancelled`。

### 18.6 出运单

```text
draft → shipped → completed
```

异常状态：`cancelled`。存在应收、应付或付款时不得直接取消，必须先调整或反冲。

## 19. 菜单和页面

### 19.1 删除或替换

- 删除“市场管理”菜单和页面入口。
- “Products”改为“客户历史商品”。
- “SO”改为“客户汇总单”。
- 原 `Supplier` 页面显示为“商品供应商”。

### 19.2 新增菜单

- 客户进口商资料
- 标签与唛头模板
- 供应商送货通知
- 仓库与库位
- 仓库库存
- 物流服务商
- 客户应收
- 供应商应付
- 收付款记录
- 财务调整单

### 19.3 装柜库存选择器

筛选条件固定包含：

- 客户
- 仓库
- 客户货号
- 客户条码
- 商品名称
- PO
- 商品供应商
- 收货单
- 原汇总单
- 批次
- 库位

列表必须显示：在库、已锁定、可用、箱数、体积、重量和来源。

## 20. API 设计原则

### 20.1 命令型接口

关键状态变更使用明确命令接口，而不是通用 PUT 修改状态：

- `POST /api/customer-orders/{id}/confirm`
- `POST /api/customer-orders/{id}/reopen`
- `POST /api/customer-orders/{id}/generate-pos`
- `POST /api/summary-orders/{id}/reserve`
- `POST /api/summary-orders/{id}/confirm`
- `POST /api/summary-orders/{id}/append-items`
- `POST /api/delivery-notices/{id}/publish`
- `POST /api/receiving-orders/{id}/confirm-arrival`
- `POST /api/qc-orders/{id}/confirm`
- `POST /api/qc-orders/{id}/unlock`
- `POST /api/container-loads/{id}/lock-inventory`
- `POST /api/container-loads/{id}/relock-inventory`
- `POST /api/container-loads/{id}/confirm`
- `POST /api/shipments/{id}/confirm-departure`
- `POST /api/finance-records/{id}/adjust`
- `POST /api/payments/{id}/reverse`

### 20.2 幂等键

以下命令必须支持幂等：

- 验货确认
- 装柜确认
- 出运确认
- 应收/应付同步
- 预收/预付自动抵扣

使用业务来源唯一索引和事务，重复请求返回已有结果，不重复生成财务或库存记录。

## 21. 权限

新增或细分权限：

- `customer_order.confirm`
- `customer_order.reopen`
- `purchase_order.confirm`
- `summary_order.confirm`
- `summary_order.append`
- `delivery_notice.publish`
- `receiving.confirm`
- `qc.confirm`
- `qc.unlock`
- `inventory.adjust`
- `container_load.lock`
- `container_load.confirm`
- `shipment.confirm_departure`
- `finance.adjust`
- `payment.reverse`
- `print_template.manage`
- `supplier_portal.publish`

后台 API 必须校验权限，前端按钮隐藏不能替代后端校验。

## 22. 审计日志

关键操作记录：

- `EntityType`
- `EntityId`
- `Action`
- `BeforeJson`
- `AfterJson`
- `Reason`
- `UserId`
- `OccurredAt`
- `CorrelationId`
- `SourceDocumentType`
- `SourceDocumentId`

验货解锁、汇总追加、库存调整、装柜确认、出运确认、财务调整和付款反冲必须记录完整快照。

## 23. 数据升级与兼容

### 23.1 升级方式

沿用现有 `DatabaseUpgradeService`：

- 增加目标版本。
- 使用可重复执行的 `CREATE TABLE IF NOT EXISTS`、`AddColumnIfMissingAsync` 和索引检查。
- 每个升级步骤可单独重试。
- 大表回填分批处理，避免启动时长事务锁表。

### 23.2 旧数据迁移

- 现有 CO/PO `DocumentLine` 按客户、商品和来源创建兼容 `OrderProduct` 快照。
- 无法确定客户条码时使用现有条码或系统 SKU，并标记 `NeedsReview`。
- 现有 `SummaryOrder` 保留单号和财务字段，但新业务明细迁移到 `summary_order_items`。
- 现有 `ContainerLoad.SummaryOrderId` 和 `Shipment.SummaryOrderId` 保留为只读兼容字段；新业务依赖行级来源和多来源关联表。
- 所有现有 `Currency` 值归一为 `RMB`，前端移除选择器。
- 市场数据不删除，只从业务菜单和新供应商流程中脱钩。

### 23.3 回滚

- 新表和新列采用向前兼容，不在首次发布中物理删除旧列。
- 旧 API 在过渡期可读，但所有新写入走新服务。
- 每个阶段发布前完成数据库备份和升级健康检查。

## 24. 错误处理

API 返回统一业务错误结构：

```json
{
  "code": "INVENTORY_NOT_AVAILABLE",
  "message": "部分库存已被其他装柜单占用",
  "details": [
    {
      "inventoryLotId": 123,
      "requestedCartons": 10,
      "availableCartons": 6
    }
  ]
}
```

核心错误码：

- `ORDER_LOCKED`
- `PO_QUANTITY_EXCEEDED`
- `SUMMARY_RESERVATION_CONFLICT`
- `DELIVERY_NOTICE_OVER_PLANNED`
- `RECEIVING_ALREADY_HAS_QC`
- `QC_QUANTITY_INVALID`
- `QC_UNLOCK_REASON_REQUIRED`
- `INVENTORY_NOT_AVAILABLE`
- `INVENTORY_RESERVATION_EXPIRED`
- `CONTAINER_CUSTOMER_MISMATCH`
- `CONTAINER_WAREHOUSE_MISMATCH`
- `SHIPMENT_ALREADY_EXISTS`
- `FINANCE_ALREADY_SYNCED`
- `PAYMENT_REVERSAL_REQUIRED`

## 25. 验收标准

### 25.1 订单商品

- 不同订单可保存同一现实商品的不同图片、条码、价格和包装。
- 复制历史商品不会修改历史记录。
- CO 确认后快照不可直接编辑。

### 25.2 汇总和送货

- 同一 PO 行可按整箱拆分到多个汇总单。
- 并发预留不能超过 PO 箱数。
- 汇总确认后按供应商、日期和仓库生成送货通知。

### 25.3 收货验货和应付

- 一张送货通知可多次收货。
- 一张收货单只有一张验货单。
- 只有最终实际接受数量入库并生成应付。
- 验货解锁后，已付款差额通过调整单处理。

### 25.4 库存装柜

- 装柜单只能选择一个客户和一个仓库的库存。
- 草稿锁定后其他装柜单不能使用同一库存。
- 72 小时到期自动释放且不删除草稿。
- 装柜确认只扣实际装走数量。
- 未装走商品继续留仓，可与后续收货库存直接生成新装柜单。
- 原汇总单标记已装柜且不能再次装柜。

### 25.5 出运和财务

- 一张装柜单只生成一张出运单。
- 装柜确认生成商品应收。
- 出运确认将物流费用追加到同一客户应收。
- 商品供应商和物流服务商分别生成应付。
- 客户收款和供应商付款均按时间先进先出冲销。
- 超收和超付自动形成预收、预付，并自动抵扣后续业务。

### 25.6 标签和唛头

- 客户可维护多个进口商和多个模板。
- 订单确认后模板快照不受后续模板修改影响。
- 可生成标签 PDF、唛头 PDF、打印数量 Excel 和 ZIP。
- 发布、查看和下载均可追溯。

## 26. 测试策略

### 26.1 后端

- xUnit + EF Core InMemory：领域规则和服务单元测试。
- MySQL 集成测试：唯一索引、事务、并发预留和库存锁定。
- 控制器测试：命令接口、权限和错误码。
- 幂等测试：重复确认不重复生成库存或财务记录。

### 26.2 前端

- Vitest：金额、箱数、体积、状态和按钮权限纯函数。
- Vue Test Utils：订单商品编辑、汇总分配、库存选择、费用编辑。
- Playwright：CO → PO → 汇总 → 收货 → QC → 库存 → 装柜 → 出运 → 收付款完整流程。

### 26.3 数据升级

- 空数据库全新初始化。
- 现有 V1.1.0 数据库升级。
- 重复执行升级脚本无副作用。
- 旧数据回填后来源链完整。

## 27. 非目标

本阶段不实现：

- 外币和汇率。
- 多客户混装。
- 多仓库一张装柜单。
- 一张出运单合并多个柜。
- 自动采购比价和供应商自动分单。
- 自动总账会计凭证。
- 供应商移动 App；首版使用响应式 Web 门户。
- 物理删除旧市场和币种列。

## 28. 实施拆分

为保证每阶段可独立测试和回滚，实施分为：

1. 订单商品、进口商、标签和唛头基础。
2. PO 汇总、送货通知、收货和验货。
3. 仓库、库存、装柜锁定和实际出库。
4. 一柜一出运单、物流服务商、费用和客户统一应收。
5. 先进先出收付款、预收预付、调整和反冲。
6. 菜单、供应商门户、打印资料包、数据回填和端到端验收。

每个阶段必须先写失败测试，再实现最小功能，全部测试通过后独立提交。