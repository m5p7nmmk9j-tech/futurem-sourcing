# FUTUREM Sourcing 开发规范

## 1. 技术栈
- 前端：Vue 3 + TypeScript + Vite + Element Plus
- 后端：ASP.NET Core Web API
- 数据库：MySQL 8

## 2. 代码命名
- 数据库表名使用 snake_case，例如 purchase_orders。
- 后端实体使用 PascalCase，例如 PurchaseOrder。
- 前端文件使用 PascalCase 组件名，例如 PurchaseOrderList.vue。
- 界面显示中文，代码命名英文，方便后续国际化。

## 3. 后端分层
- Controllers：接口入口
- Services：业务逻辑
- Repositories：数据访问
- Entities：数据库实体
- DTOs：请求和响应模型

## 4. 前端结构
```text
web/src
  api/
  components/
  layouts/
  router/
  stores/
  views/
    dashboard/
    customers/
    suppliers/
    products/
    rfq/
    co/
    po/
    so/
    receiving/
    qc/
    logistics/
    finance/
```

## 5. 单据页面统一工具栏
所有 RFQ、CO、PO、SO、收货、QC、装柜、出运页面统一：
- 新增
- 保存
- 复制
- 删除/作废
- 增加商品
- 增加历史商品
- 打印
- 导出 PDF/Excel

## 6. Git 提交规范
- feat: 新功能
- fix: 修复问题
- docs: 文档
- refactor: 重构
- chore: 工程配置

示例：
- feat: add product management
- docs: update PRD
- fix: purchase order copy calculation
