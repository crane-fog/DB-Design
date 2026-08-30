# 前端公共基础整合说明

## 公共契约

- 分页唯一来源：`src/services/pagination.ts` 中的 `PageRequest` 与 `PageResult<T>`。页面只接收 `{ items, page, pageSize, total }`。
- API 信封唯一由 `unwrap` 处理。当前 OpenAPI 契约统一为 `{ code, message, data }`；分页 DTO 的 `records` / `page_size` 仅在该公共解析层兼容，页面不会读取这些字段。
- Service 负责 DTO 到页面模型的映射、参数清洗与错误传递。`cleanQuery` 会 trim 文本并省略空字符串和 `undefined`；除非后端契约明确要求，Service 不发送 `null`。
- API 或业务错误会被抛出，`getRequestErrorMessage` 保留后端 `message`，公共层不弹出通知、不降级为 Mock。

## 跨模块 ID 字段表

| 实体 | 当前字段 | 推荐字段 | 涉及模块 | 状态 / 迁移方案 |
| --- | --- | --- | --- | --- |
| 物料 | `materialId` | `materialId` / `materialCode` | 物料、库存、采购、生产、追溯 | 已统一 ID；业务编号待物料 API 契约确认 |
| BOM | `bomId`、`bomCode` | `bomId` / `bomCode` | 物料 | 已统一 |
| BOM 版本 | `versionId`、`version` | `bomVersionId` / `versionNo` | 物料、库存、生产 | 待后端契约确认；当前局部字段保留 |
| 采购订单 | `orderId` | `purchaseOrderId` / `purchaseOrderNo` | 采购、库存、追溯 | 需组员修改；跨模块前先确认 API 字段 |
| 采购明细 | `itemId` | `purchaseOrderItemId` | 采购、追溯 | 待后端契约确认 |
| 生产订单 | `orderId` | `productionOrderId` / `productionOrderNo` | 生产、库存、追溯 | 需组员修改；现有局部字段保持兼容 |
| 供应商 | `supplierId` | `supplierId` / `supplierCode` | 采购、追溯 | ID 已统一；编号待契约确认 |
| 批次 | `batchNo` | `batchId` / `batchNo` | 库存、追溯 | 待后端确认 `batchId`；禁止用 `batchNo` 作为关联主键 |
| 仓库 / 库位 | 未见跨模块字段 | `warehouseId` / `locationId` 及 Code | 库存 | 暂不引入，等待后端契约 |

ID 是内部关联主键，Code/No 是业务可读编号，Name 仅展示。`orderId`、`itemId`、`batchId` 不得在新增跨模块接口中脱离实体语义使用。

## 权限矩阵

| 模块 | 页面 / 功能 | 菜单与路由权限 | 按钮权限 | 当前状态 |
| --- | --- | --- | --- | --- |
| 物料 | BOM 概览 | `material:view` | 无维护按钮 | 已集中；`material:manage` 为预留维护权限 |
| 库存 | 概览、计算、监控、登记 | `inventory:view`、细分页权限 | 细分页权限 | 已集中；`inventory:manage` 待后端授权模型确认 |
| 采购 | 采购概览 | `purchase:view` | `purchase:manage` | 已修正，查看不再等同管理 |
| 生产 | 概览、产能、订单、故障 | `production:view`、细分页权限 | 现有细分页权限 | 保留当前细粒度；`production:manage` 为预留 |
| 追溯 | 追溯概览 | `trace:view` | `trace:manage` | 已集中且管理按钮正确使用 manage |
| 系统 | 系统、用户、角色、审计 | `system:*:view` | 对应 create/update/assign | 保持既有逻辑 |

权限常量位于 `src/constants/permissions.ts`。本地 `DEV_ADMIN` 由该常量展开，覆盖所有已注册的前端权限；`DEV_USER` 仅含业务查看权限。

## 状态与 Mock

状态展示映射集中在 `src/constants/status/index.ts`，涵盖物料 BOM、库存监控、采购订单/催交和生产订单。未知状态由 `StatusTag` 回退为原始值和中性样式，不作为业务判断条件。

| 模块 | 数据源 | 开关 | 真实 API | 自动回退 |
| --- | --- | --- | --- | --- |
| 工作台 | 集中 Mock | `VITE_DATA_MODE=mock` | 未接入 | 否 |
| 物料 BOM | 集中 Mock | `VITE_DATA_MODE=mock` | 未接入 | 否，`api` 模式明确报错 |
| 库存 | Mock 或 API | `VITE_DATA_MODE` | 支持 | 否 |
| 采购 | Mock 或 API | `VITE_DATA_MODE` | 支持 | 否 |
| 生产、追溯 | API | 无 | 支持 | 否 |

`VITE_USE_MOCK_AUTH` 仅在 Vite 开发环境生效；`VITE_DATA_MODE` 控制业务 Service 的全局数据源，生产构建不会因该模式自动回退 Mock。联调完成后删除已被真实 API 覆盖的 Service Mock 分支和配置数据。

## 待处理事项

- 模块成员：把跨模块 `orderId` 和 `itemId` 改为实体化名称，变更前同步 API 契约。
- 后端：确认 BOM 版本、批次、仓库/库位和业务编号字段；当前没有可靠证据时不做猜测性重命名。
- 收口阶段：在每个模块 API 联调完成后移除相应 Mock，不补做本次范围外的业务流程或页面重构。
