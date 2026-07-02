# FUTUREM Sourcing 更新日志

## 2026-07-03

### docs: initialize project documents
- 新增 README.md
- 新增 PRD 产品需求文档
- 新增数据库设计文档
- 新增业务流程文档
- 新增开发规范文档

### 已锁定的核心规则
- FUTUREM Sourcing 与 FUTUREM ERP 完全独立。
- 商品资料不保存价格、包装、CBM、KG。
- 采购价格放 PO，销售价格放 CO。
- 包装、体积、重量放订单明细。
- 客户收款按 SO，供应商付款按 PO。
- 单号使用前缀 + 年月日时分秒。
- 全部软删除。
- 所有单据支持复制后修改。
