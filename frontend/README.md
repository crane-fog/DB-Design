# 前端开发说明

## 环境与命令

本项目使用 Vue 3、TypeScript、Vite、Element Plus 和 pnpm。`package.json` 指定 Node.js `^20.19.0 || >=22.12.0`，推荐使用当前 LTS 的 Node.js 22；pnpm 版本由 `packageManager` 固定为 `10.32.1`。

```bash
cd frontend
pnpm install
pnpm dev
```

日常检查命令如下：

```bash
pnpm lint:ci                 # 只检查，不改文件
pnpm lint                    # 自动修复可修复的 lint 问题
pnpm format:ci               # 检查 Prettier 格式
pnpm format                  # 格式化 src/
pnpm type-check              # vue-tsc --build
pnpm exec vue-tsc --noEmit   # 单次 TypeScript/Vue 类型检查
pnpm build                   # 类型检查后执行 Vite 构建
```

当前 `package.json` 没有测试脚本；新增测试框架或测试脚本前应先与团队确认。构建会生成 `dist/`，该目录只用于验证，不提交到仓库。

## 环境变量

复制示例文件后按本机环境调整：

```bash
copy .env.example .env
```

- `.env`：本机通用配置，不得包含密码、Token 或个人凭据，也不得提交。
- `.env.development`：仅开发模式使用的覆盖项。
- `.env.production`：仅生产构建使用的公开配置；所有 `VITE_` 前缀变量会被打包到浏览器，不能存放秘密。
- `VITE_API_BASE_URL`：生成 API 客户端的基址。默认留空，接口仍以同源 `/api` 路径访问。
- `VITE_API_PROXY_TARGET`：Vite 开发代理的目标地址，默认 `http://localhost:5000`。
- `VITE_USE_MOCK_AUTH`：只在 Vite 开发环境启用本地 Mock 登录；即使生产环境设为 `true`，生产构建也会禁用。

页面只能通过 Service 调用 API，禁止在页面中硬编码完整接口地址。新增环境变量时先补充 `.env.example` 和本节说明；不要提交 `.env`、Token、密码或个人本地配置。

## 本地 Mock 登录

当 Oracle 或后端暂不可用时，在本机 `.env` 中设置后重启 Vite：

```env
VITE_USE_MOCK_AUTH=true
```

可使用以下公开测试账号登录：

```text
DEV_ADMIN / dev-admin-123
DEV_USER / dev-user-123
```

- `DEV_ADMIN` 拥有当前前端全部已注册权限，可进入系统管理、用户管理、角色管理和审计日志。
- `DEV_USER` 仅拥有普通业务查看权限；不显示系统管理，访问 `/system` 会进入 403。
- 设置 `VITE_USE_MOCK_AUTH=false` 后，登录会恢复调用真实 `/api/login`，请求体与密码哈希流程不变。

Mock 账号仅用于本地前端开发，密码属于可公开的测试数据，不能替代真实账号。Mock 登录要求同时满足 `import.meta.env.DEV` 和 `VITE_USE_MOCK_AUTH=true`；因此生产构建无法启用该入口，也不得将真实账号、密码、Token 或数据库连接写入仓库。

## Mock 与 Service 规范

- 页面只能调用 `src/services/`，禁止直接调用 Axios、自动生成 API 或在模板中编写大型 Mock 数组。
- 工作台 Mock 集中放在 `src/config/dashboard-mock.ts`，类型位于 `src/types/dashboard.ts`，唯一调用入口为 `DashboardService.ts`。
- Mock 与未来接口共用同一套 TypeScript 类型。列表/分页统一使用 `{ items, total, page, pageSize }`；成功数据由 Service 返回该业务类型，失败必须 `throw Error` 供页面捕获。
- 当前工作台没有 OpenAPI 契约接口，因此 Service 使用集中 Mock。后端契约和实现可用后，只替换 Service 内部实现，不改页面和类型。
- `VITE_DASHBOARD_MOCK_SCENARIO` 可设为 `success`、`empty` 或 `error`，用于验证正常、空数据和失败重试状态；修改后重启 `pnpm dev`。未配置时为稳定的 `success` 数据。
- 只有已知“后端尚未实现且返回 404”的权限联调链路可以降级到既有 Mock；其他 404、网络错误和前端运行时错误必须如实显示，不能被 Mock 掩盖。
- 当真实接口已经覆盖页面所需字段、分页和错误语义，并完成联调验证后，删除对应 Mock 降级分支。

## 页面开发规范

新增页面按以下顺序完成：

```text
创建页面
→ 注册路由
→ 配置 meta
→ 添加菜单
→ 配置权限标识
→ 创建 Service 和类型
→ 补齐加载、空状态、错误和重试
→ 执行检查命令
```

- 不直接编辑 `src/api/` 自动生成代码；仅 `src/api/client.ts` 可以由统一维护者修改。
- 页面不直接调用 Axios，不硬编码账号、角色或权限集合；权限判断必须复用 `useAuthStore().hasPermission` 或已有统一工具。
- 路由 `meta.permission`、菜单权限和按钮/入口权限必须使用同一权限标识。
- 表单提交必须维护提交中状态以防重复提交；删除、停用、重置密码等不可逆或高风险操作必须二次确认。
- 表格和概览页都必须处理加载、空数据、请求失败和重试。异步请求在组件卸载后不得更新页面状态，也要避免重复或无限请求。
- 复用 `PageContainer`、`PageHeader`、`EmptyState` 等公共组件，并保持 Element Plus 的后台视觉风格。

## Git 协作

- 在非 `main` 分支开发，分支建议使用 `feat/<scope>`、`fix/<scope>`、`docs/<scope>`；本地创建分支时使用 `codex/` 前缀。
- Commit 使用 Commitizen/Conventional Commits 格式，例如 `feat: add dashboard overview`、`fix: correct permission redirect`。
- 提交前先执行 `pre-commit install`，再依次执行 `git fetch origin`、`git rebase origin/main`、`pnpm lint:ci`、`pnpm format:ci`、`pnpm exec vue-tsc --noEmit`、`pnpm build` 和 `git diff --check`。
- 合并前必须同步目标分支；解决冲突后重新执行全部检查。不得绕过 pre-commit，不得未经检查直接推送到主分支。
- 禁止提交 `.env`、`dist/`、本地数据库文件、Token、密码和其他机器相关产物。推送分支后请通过 PR 合并到 `main`。
