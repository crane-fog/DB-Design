# 前端公共基础整合说明

## 公共契约

- 分页唯一来源：`src/services/pagination.ts` 中的 `PageRequest` 与 `PageResult<T>`。页面只接收 `{ items, page, pageSize, total }`。
- API 信封唯一由 `unwrap` 处理。当前 OpenAPI 契约统一为 `{ code, message, data }`；分页 DTO 的 `records` / `page_size` 仅在该公共解析层兼容，页面不会读取这些字段。
- Service 负责 DTO 到页面模型的映射、参数清洗与错误传递。`cleanQuery` 会 trim 文本并省略空字符串和 `undefined`；除非后端契约明确要求，Service 不发送 `null`。
- API 或业务错误会被抛出，`getRequestErrorMessage` 保留后端 `message`，公共层不弹出通知、不替换为本地数据。

## 跨模块 ID 字段表

| 实体 | 前端字段 | API 字段 | 说明 |
| --- | --- | --- | --- |
| 物料 | `materialId`，物料页的 `id` / `code` | `material_id` | 物料页显示后端分配的数字 ID，不生成独立业务编号 |
| BOM 明细 | `componentId` | `bom_id` | 与版本 ID 区分 |
| BOM 版本 | 物料页的 `bomId`，其他模块的 `versionId` | `version_id` | 版本号为 `version_no`，当前版本由物料的 `current_version_id` 指定 |
| 采购订单 | `orderId` | `order_id` | 仅在采购语境中使用 |
| 采购明细 | `itemId` | `item_id` | 用于批次消耗和原材料追溯 |
| 生产订单 | `orderId` | `order_id` | 仅在生产、完工入库和成品追溯语境中使用 |
| 供应商 | `supplierId` | `supplier_id` | 来源于后端物料详情或采购订单 |
| 成品批次 | `batchNo` | `batch_no` | 使用后端入库和追溯接口返回的批号 |

跨模块传递 ID 时必须明确实体语义，不能将采购订单 ID 和生产订单 ID 混用。Name 仅用于展示，不作为关联键。

## 权限矩阵

| 模块 | 页面 / 功能 | 菜单与路由权限 | 按钮权限 | 当前状态 |
| --- | --- | --- | --- | --- |
| 物料 | BOM 概览 | `material:view` | `material:manage` | 维护物料、版本和组件，切换当前版本 |
| 库存 | 概览、计算、监控、登记 | `inventory:view`、细分页权限 | 细分页权限 | 无权读取辅助目录时可输入真实 ID |
| 采购 | 采购概览 | `purchase:view` | `purchase:manage` | 已修正，查看不再等同管理 |
| 生产 | 概览、产能、订单、故障 | `production:view`、细分页权限 | 现有细分页权限 | 与后端生产管理员和外部客户角色匹配 |
| 追溯 | 追溯概览 | `trace:view` | `trace:manage` | 已集中且管理按钮正确使用 manage |
| 系统 | 系统、用户、角色、审计 | `system:*:view` | 对应 create/update/assign | 保持既有逻辑 |

权限常量位于 `src/constants/permissions.ts`。角色和权限来自 `/api/getCurrentAccess`；Service 将后端返回的业务角色转换为对应页面权限，系统管理员可访问全部前端功能，实际请求仍由后端鉴权。

## 状态与数据来源

状态展示映射集中在 `src/constants/status/index.ts`，涵盖库存监控、采购订单/催交和生产订单。BOM 页面展示真实有效期和当前版本，不引入额外版本状态。未知状态由 `StatusTag` 回退为原始值和中性样式，不作为业务判断条件。

| 模块 | 数据来源 |
| --- | --- |
| 工作台 | 汇总真实业务接口、系统统计和审计记录 |
| 物料管理 | 物料、分类、BOM 版本、结构和需求分析接口 |
| 库存、采购 | 库存、入库、采购订单和催交接口 |
| 生产、追溯 | 生产订单、产能、故障反馈及批次追溯接口 |
| 登录与系统管理 | 登录、当前权限和系统管理接口 |

所有环境都直接访问真实后端，不提供数据源切换或本地业务数据持久化。API 基址由 `VITE_API_BASE_URL` 配置，开发代理使用 `VITE_API_PROXY_TARGET`。
