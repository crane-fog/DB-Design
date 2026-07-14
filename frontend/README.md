# 前端开发说明

## 启动与校验

```bash
pnpm install
pnpm dev
pnpm type-check
pnpm lint:ci
pnpm format:ci
pnpm build
```

开发时前端会将 `/api` 代理到本机 `http://localhost:5000`。需要自动修复格式或 lint 问题时，分别执行 `pnpm format` 与 `pnpm lint`。

## 目录职责

- `src/layouts/`：后台应用壳层；`AdminLayout.vue` 由组长维护。
- `src/components/common/`：页面容器、页头、查询区、状态标签、加载、空状态和确认弹窗等公共组件。
- `src/pages/`：按业务模块放置页面。业务成员主要在各自目录中开发。
- `src/stores/`：Pinia 状态；`auth.ts` 统一维护 JWT 和基础用户状态。
- `src/services/`：页面调用 API 的入口，负责隔离自动生成的 API 与页面逻辑。
- `src/utils/`：存储、格式化、权限和错误消息等纯工具。
- `src/api/`：OpenAPI 自动生成代码，禁止手动修改；只有 `src/api/client.ts` 可由组长统一维护。

## API 规则

1. 禁止修改 `src/api/` 中的自动生成文件；不要在页面中创建 Axios 实例。
2. 仅 `src/api/client.ts` 创建 Axios 实例，统一添加 Bearer Token 和处理 HTTP 401/403。
3. 页面优先调用 `src/services/`；不要写死后端地址或直接散落调用生成 API。
4. 新接口生成后，在对应 Service 新增清晰的方法，再由页面调用。`UsersPage.vue` 的 `systemService.listUsers` 是参考实现。

## 页面结构与状态

列表页按以下顺序组织：`PageHeader`、`SearchPanel`、操作区、表格或内容区、分页、弹窗/详情区。

每个业务页面至少应明确处理：加载中、空数据、请求失败、操作成功和操作失败。不能把占位页面或测试数据表述为已完成业务功能。

## 协作边界

- 物料/BOM 成员：主要维护 `src/pages/materials/` 与 `src/services/MaterialService.ts`。
- 库存/采购成员：主要维护 `src/pages/inventory/`、`src/pages/purchase/` 及对应 Service。
- 生产成员：主要维护 `src/pages/production/` 与 `src/services/ProductionService.ts`。
- 质量追溯与系统页按模块目录维护，并参考 `src/pages/system/UsersPage.vue` 的查询、表格、状态、分页、加载和错误处理方式。

以下公共文件原则上由组长维护，组员修改前先沟通：`App.vue`、`router/`、`layouts/`、`components/common/`、`stores/`、`api/client.ts`、`styles/`、`package.json`、`vite.config.ts`。

提交前须在非 `main` 分支完成 `pre-commit install`，同步 `origin/main` 后按项目规范完成检查。不得修改 CI、pre-commit 或构建配置来规避检查。
