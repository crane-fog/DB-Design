# 数据库课程设计

## 前端本地开发

前端位于 `frontend/`，使用 Vue 3、TypeScript、Vite、Element Plus 和 pnpm。

```bash
cd frontend
pnpm install
pnpm dev
```

库存与采购契约已经生成客户端，但对应后端控制器尚未实现。本地开发可在 `.env` 中启用集中 Mock：

```env
VITE_USE_INVENTORY_PURCHASE_MOCK=true
```

该开关仅在 Vite 开发环境生效。设为 `false` 后，`InventoryService` 和 `PurchaseService`
会调用 OpenAPI 生成客户端；页面不需要改动。Mock 数据分别集中在
`src/config/inventory-mock.ts` 和 `src/config/purchase-mock.ts`。

提交前执行：

```bash
pnpm lint:ci
pnpm format:ci
pnpm exec vue-tsc --noEmit
pnpm build
```
