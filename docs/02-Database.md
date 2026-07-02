# FUTUREM Sourcing 数据库设计

## 1. 数据库原则
- MySQL 8。
- 内部主键使用 BIGINT AUTO_INCREMENT。
- 业务单号使用时间到秒编号，例如 PO20260701142356。
- 所有主表包含：id、no、status、created_by、created_at、updated_by、updated_at、is_deleted、remark。
- 所有业务数据软删除，不物理删除。
- 商品表不保存价格、包装、CBM、KG。
- 价格和包装全部保存在单据明细表。

## 2. V1.0 核心表，约 28 张

### 基础资料
1. users
2. roles
3. user_roles
4. customers
5. suppliers
6. markets
7. shops
8. products
9. product_categories
10. dictionaries

### 业务单据
11. buying_trips
12. rfqs
13. rfq_items
14. supplier_quotations
15. customer_orders
16. customer_order_items
17. purchase_orders
18. purchase_order_items
19. summary_orders
20. summary_order_purchase_orders

### 仓库与物流
21. receiving_orders
22. receiving_items
23. qc_orders
24. qc_items
25. container_loads
26. container_load_items
27. shipments

### 财务与日志
28. finance_records
29. operation_logs

## 3. 关键表说明

### products 商品表
只保存固定信息：SKU、条码、图片、名称、分类、品牌、客户货号、单位。
不保存采购价、销售价、包装、体积、重量。

### customer_order_items 客户订单明细
保存销售价、销售金额、数量、包装参数、CBM、KG。

### purchase_order_items 采购订单明细
保存采购价、采购金额、数量、装箱数量、外箱长宽高、单箱 CBM、单箱毛重、总 CBM、总 KG、已收数量。

### summary_orders 汇总单
一个客户一次采购的汇总单，可关联多张 PO。客户收款以 SO 为依据。

### summary_order_purchase_orders
SO 与 PO 的多对多关系表。

## 4. 历史价格与历史包装
不单独建历史价格表，也不单独建包装历史表。
历史采购价和历史包装从 purchase_order_items 查询。
历史销售价从 customer_order_items 查询。
点击历史记录时跳转到来源订单。
