# FUTUREM Enterprise Sourcing ERP

FUTUREM Enterprise 是一套面向外贸公司、采购跟单团队、跨境贸易公司的企业级 ERP / Sourcing 系统。

## 核心业务流程

```text
RFQ 询价
  ↓
CO 客户订单
  ↓
PO 采购订单
  ↓
Receiving 收货
  ↓
QC 质检
  ↓
SO 销售/订仓汇总
  ↓
Container 装柜
  ↓
Shipment 出运
  ↓
Finance 应收应付
  ↓
Payment 收付款
```

## 已完成功能

- 基础资料：客户、供应商、商品、市场
- 外贸业务：RFQ、CO、PO、SO、收货、QC、装柜、出运
- 财务系统：应收、应付、收款、付款、银行账户
- BI 经营分析：利润、客户排名、供应商排名、商品排名、趋势、KPI、漏斗
- 老板驾驶舱 V2：经营指标、KPI 仪表盘、流程漏斗、预警中心
- 消息中心：业务预警生成、未读统计、标记已读
- 审批流：提交、通过、驳回、退回、多步骤审批
- RBAC 权限：角色、权限、用户、角色授权、数据权限范围
- JWT 登录：Access Token、Refresh Token、Session、Login Log
- 操作日志：新增、修改、删除、审批、导出、打印、登录等记录
- 打印中心：PO、SO、PI、Invoice、Packing、QC、Receiving、Container、Shipment、Payment
- Excel 中心：模板下载、CSV 导出、CSV 上传解析
- 系统参数：公司、汇率、编号规则、港口、付款方式、SMTP、WhatsApp、备份参数
- 全局搜索：SKU、客户、供应商、PO、SO、装柜、出运、财务
- 数据库版本管理：Schema Version、Migration History、启动检查
- Redis 缓存：状态检查、按范围清理、不可用自动降级
- 备份中心：备份任务、手动备份、备份历史、恢复前校验
- 系统监控：API、MySQL、Redis、磁盘、内存、日志、备份状态
- Docker 部署：MySQL + Redis + API + Web + Nginx
- CI 检查：API Build、Web Build、Docker Compose Build

## 技术架构

- Backend: .NET 9 Web API
- Frontend: Vue 3 + Element Plus
- Database: MySQL 8.0
- Cache: Redis 7
- Deployment: Docker Compose
- Reverse Proxy: Nginx

## Docker 一键启动

### Mac / Linux

```bash
chmod +x scripts/start-docker.sh scripts/stop-docker.sh scripts/check.sh
./scripts/start-docker.sh
```

### Windows

双击：

```text
scripts/start-docker.bat
```

## 访问地址

```text
Web:   http://localhost:3000
API:   http://localhost:8080
MySQL: localhost:3307
Redis: localhost:6379
```

## 默认数据库

```text
Database: futurem_sourcing
User: root
Password: futurem123456
```

可通过环境变量修改：

```bash
MYSQL_ROOT_PASSWORD=your_password docker compose up -d --build
```

## 初始化建议

系统启动后，建议在 Swagger 中依次执行：

```text
POST /api/auth/seed-admin
POST /api/system-settings/seed
POST /api/print-center/seed
POST /api/backup-center/seed
```

默认管理员：

```text
Username: admin
Password: Admin@123456
```

## 编译检查

Mac / Linux：

```bash
./scripts/check.sh
```

Windows：

```text
scripts/check.bat
```

## 版本状态

```text
Version: V1.0.0 Enterprise Release Candidate
Status: Core ERP completed. CI and release validation in progress.
```

## V1.0 Release 验收清单

- API 编译通过
- Web 编译通过
- Docker Compose 构建通过
- MySQL 初始化通过
- Redis 状态检查通过
- JWT 登录通过
- 全业务流程测试通过
- 打印中心测试通过
- Excel 中心测试通过
- 备份中心测试通过
- 系统监控测试通过

## 后续路线

- V1.0.0 Enterprise Release
- V1.1.0 WMS 仓储增强
- V1.2.0 MRP 物料需求计划
- V1.3.0 生产管理
- V1.4.0 CRM 客户增强
- V1.5.0 AI 智能采购助手
- V2.0.0 集团版 / 多账套
