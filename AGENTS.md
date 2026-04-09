# Agent Guide

本文档为面向 Agent 的项目参考及执行规范。

## 1. 项目概览

本项目是一个以 Oracle 数据库设计为核心的前后端分离项目。使用 `pre-commit` 进行代码质量检查，使用 GitHub Actions 实现 CI/CD 流水线，使用 OpenAPI Generator 从 API 契约自动生成前后端接口代码。

- 前端：TypeScript + Vue 3（目录 `frontend/`），使用 pnpm 包管理器
- 后端：C# + .NET 8（目录 `backend/`）
- API 契约：`api/openapi.yaml`
- CI/CD 流水线配置：`.github/workflows/`，不得修改

以下文件在 CI/CD 流水线中由 OpenAPI Generator 生成，不得直接修改：

- 前端：`frontend/src/api/*`，主要内容为对接后端的 API 调用。
- 后端：`backend/Converters/*`、`backend/Models/*`，主要内容为根据 API 契约生成的数据模型，需要手动适配业务逻辑。

由开发者编写，可以直接修改的文件主要位于：

- 前端：`frontend/src/`
- 后端：`backend/Controllers/`、`backend/Services/`、`backend/Program.cs` 等

## 2. Agent 执行规范

无论任务看起来多小，Agent 都要判断改动类型：前端 / 后端 / API 契约，然后按以下流程执行。

### 2.1 改前端

1. 在 `frontend/src/` 中做修改。
2. 在 `frontend` 目录下依次执行 `pnpm lint`、`pnpm format`、 `pnpm build`。

### 2.2 改后端

1. 在 `backend/Services/`、`backend/Program.cs` 等位置做修改。
2. 在 `backend` 目录下依次执行 `dotnet format backend.sln`、`dotnet build -warnaserror --no-incremental`。

### 2.3 改 API 契约（谨慎执行）

1. 先改 `api/openapi.yaml`。
2. 检查当前不在 `main` 分支。
3. 提交 git 并 push 到当前分支，触发 CI/CD 流水线 `gen-api-code.yml`。
4. 等待流水线自动提交生成代码到当前分支。
5. 执行 `git pull` 拉取生成后的代码。
6. 对后端代码做适配：当前流水线设定中，后端仅生成 API 数据模型，需要修改 `backend/Controllers/*` 下的 API 路由来添加业务逻辑。
7. 对前端代码做适配：根据生成的 API 代码修改 `frontend/src/` 下的代码来调用新接口。
8. 运行前后端必要构建测试。

### 2.4 git 提交规范

1. 必须在非 main 分支进行开发，在开始前检查当前分支。
2. 每次提交必须包含简洁但明确的 commit message，需遵循 Commitizen 规范（例如：`feat: add user login`、`fix: correct typo in database schema`）。
3. 进行 git 提交前先执行 `pre-commit install` 确保 git hooks 已安装。
4. 进行 git 提交前先依次执行 `git fetch origin`、`git rebase origin/main`，确保分支与 main 的同步，避免出现混乱的提交历史。
5. 如提交被 `pre-commit` 检查拦截，必须修复问题后再提交，不得绕过检查。
6. 在提交了一些 commit 并 push 到远程分支后，引导用户发起 PR 到 main 分支。

## 3. 禁止项

1. 禁止直接编辑自动生成的代码。
2. 禁止以任何形式绕过 `pre-commit` 检查。
3. 禁止修改 `.pre-commit-config.yaml`、`.github/workflows/*` 等检查配置文件来规避检查。
4. 禁止通过修改 `frontend/package.json`、`backend/*.csproj`、`backend/.editorconfig` 等构建配置文件来规避检查。

## 4. Agent 交付输出模板

Agent 结束任务时应明确汇报：

1. 改了哪些文件（按 前端 / 后端 / API 分类）。
2. 跑了哪些命令。
3. 每条命令是否通过。
4. 做出的修改可能导致哪些风险。

若任一关键命令失败，不应输出“完成”，而应继续修复直至通过或明确阻塞原因。
