## 数据库课程设计

### 前端

需要 pnpm

```sh
cd frontend
pnpm install
pnpm dev
```

### 后端

需要 Visual Studio 2022，工作负荷：.NET 桌面开发

```sh
cd backend
cp .env.example .env
```

编辑 `.env`，配置数据库连接字符串

```sh
dotnet build
dotnet run
```
