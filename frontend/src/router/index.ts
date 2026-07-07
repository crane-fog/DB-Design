import { type RouteRecordRaw, createRouter, createWebHistory } from 'vue-router'

export type ModuleKey = 'materials' | 'inventory' | 'purchase' | 'production' | 'trace' | 'system'

export interface ModuleSubPage {
  key: string
  path: string
  title: string
}

export interface ModulePage {
  key: ModuleKey
  path: string
  subPages: ModuleSubPage[]
  title: string
}

export const pages: ModulePage[] = [
  {
    key: 'materials',
    path: '/materials',
    subPages: [],
    title: '物料管理',
  },
  {
    key: 'inventory',
    path: '/inventory',
    subPages: [
      { key: 'calc', path: '/inventory/calc', title: '物料缺口计算' },
      { key: 'monitor', path: '/inventory/monitor', title: '库存监控' },
      { key: 'register', path: '/inventory/register', title: '完工入库登记' },
    ],
    title: '库存管理',
  },
  {
    key: 'purchase',
    path: '/purchase',
    subPages: [],
    title: '采购管理',
  },
  {
    key: 'production',
    path: '/production',
    subPages: [
      { key: 'capacity', path: '/production/capacity', title: '产能配置' },
      { key: 'orders', path: '/production/orders', title: '生产订单' },
      { key: 'breakdown', path: '/production/breakdown', title: '故障反馈' },
    ],
    title: '生产管理',
  },
  {
    key: 'trace',
    path: '/trace',
    subPages: [],
    title: '质量追溯',
  },
  {
    key: 'system',
    path: '/system',
    subPages: [
      { key: 'users', path: '/system/users', title: '账号管理' },
      { key: 'audit-logs', path: '/system/audit-logs', title: '操作审计' },
    ],
    title: '系统管理',
  },
]

const routes: RouteRecordRaw[] = [
  {
    component: () => import('@/pages/HomePage.vue'),
    name: 'home',
    path: '/',
  },
  {
    component: () => import('@/pages/materials/MaterialsOverview.vue'),
    meta: { moduleKey: 'materials' },
    name: 'materials',
    path: '/materials',
  },
  {
    component: () => import('@/pages/inventory/InventoryOverview.vue'),
    meta: { moduleKey: 'inventory' },
    name: 'inventory',
    path: '/inventory',
  },
  {
    component: () => import('@/pages/inventory/CalcPage.vue'),
    meta: { moduleKey: 'inventory', subPageKey: 'calc' },
    name: 'calc',
    path: '/inventory/calc',
  },
  {
    component: () => import('@/pages/inventory/MonitorPage.vue'),
    meta: { moduleKey: 'inventory', subPageKey: 'monitor' },
    name: 'monitor',
    path: '/inventory/monitor',
  },
  {
    component: () => import('@/pages/inventory/RegisterPage.vue'),
    meta: { moduleKey: 'inventory', subPageKey: 'register' },
    name: 'register',
    path: '/inventory/register',
  },
  {
    component: () => import('@/pages/purchase/PurchaseOverview.vue'),
    meta: { moduleKey: 'purchase' },
    name: 'purchase',
    path: '/purchase',
  },
  {
    component: () => import('@/pages/production/ProductionOverview.vue'),
    meta: { moduleKey: 'production' },
    name: 'production',
    path: '/production',
  },
  {
    component: () => import('@/pages/production/CapacityPage.vue'),
    meta: { moduleKey: 'production', subPageKey: 'capacity' },
    name: 'capacity',
    path: '/production/capacity',
  },
  {
    component: () => import('@/pages/production/OrdersPage.vue'),
    meta: { moduleKey: 'production', subPageKey: 'orders' },
    name: 'orders',
    path: '/production/orders',
  },
  {
    component: () => import('@/pages/production/BreakdownPage.vue'),
    meta: { moduleKey: 'production', subPageKey: 'breakdown' },
    name: 'breakdown',
    path: '/production/breakdown',
  },
  {
    component: () => import('@/pages/trace/TraceOverview.vue'),
    meta: { moduleKey: 'trace' },
    name: 'trace',
    path: '/trace',
  },
  {
    component: () => import('@/pages/system/SystemOverview.vue'),
    meta: { moduleKey: 'system' },
    name: 'system',
    path: '/system',
  },
  {
    component: () => import('@/pages/system/UsersPage.vue'),
    meta: { moduleKey: 'system', subPageKey: 'users' },
    name: 'users',
    path: '/system/users',
  },
  {
    component: () => import('@/pages/system/AuditLogsPage.vue'),
    meta: { moduleKey: 'system', subPageKey: 'audit-logs' },
    name: 'audit-logs',
    path: '/system/audit-logs',
  },
  { path: '/:pathMatch(.*)*', redirect: '/' },
]

export const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

function hasValidToken() {
  const token = localStorage.getItem('jwt')
  const expires = Number(localStorage.getItem('expires'))

  if (!token || !Number.isFinite(expires)) {
    return false
  }

  return Date.now() < expires * 1000
}

router.beforeEach((to) => {
  if (hasValidToken()) {
    return true
  }

  localStorage.removeItem('jwt')
  localStorage.removeItem('expires')
  globalThis.location.href = `/login.html?redirect=${encodeURIComponent(to.fullPath)}`
  return false
})
