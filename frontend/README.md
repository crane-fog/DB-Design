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

页面只能通过 Service 调用 API，禁止在页面中硬编码完整接口地址。新增环境变量时先补充 `.env.example` 和本节说明；不要提交 `.env`、Token、密码或个人本地配置。

## 后端连接与 Service 规范

登录、权限初始化和所有业务操作均调用真实后端。开发前先启动后端及其数据库，并配置 API 基址或开发代理地址；登录需要真实账号。

- 登录调用 `/api/login`，密码按现有流程进行 SHA-256 哈希；权限通过 `/api/getCurrentAccess` 获取，每次加载应用都会重新校验。
- 页面只能调用 `src/services/`，由 Service 使用生成 API 客户端和统一鉴权、错误处理。
- 列表/分页统一使用 `{ items, total, page, pageSize }`；成功数据由 Service 返回业务类型，失败必须抛出错误供页面展示和重试。
- 工作台通过真实业务接口汇总统计、待办和审计记录，只请求当前用户有权查看的数据。
- 网络或业务错误会如实显示，不会切换到本地数据或返回虚构的成功结果。

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
