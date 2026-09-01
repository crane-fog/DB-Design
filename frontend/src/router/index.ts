import { PERMISSIONS, type PermissionCode } from '@/constants/permissions'
import { type RouteRecordRaw, createRouter, createWebHistory } from 'vue-router'
import { pinia } from '@/stores/pinia'
import { systemService } from '@/services/SystemService'
import { useAuthStore } from '@/stores/auth'

declare module 'vue-router' {
  interface RouteMeta {
    icon?: string
    isModule?: boolean
    module?: string
    pageOrder?: number
    permission?: PermissionCode
    requiresAuth?: boolean
    showInMenu?: boolean
    title?: string
  }
}

const adminRoutes: RouteRecordRaw[] = [
  {
    component: () => import('@/pages/HomePage.vue'),
    meta: {
      isModule: true,
      module: 'home',
      pageOrder: 0,
      requiresAuth: true,
      showInMenu: true,
      title: '工作台',
    },
    name: 'home',
    path: '',
  },
  {
    component: () => import('@/pages/materials/MaterialsOverview.vue'),
    meta: {
      icon: 'materials',
      isModule: true,
      module: 'materials',
      pageOrder: 1,
      permission: PERMISSIONS.material.view,
      requiresAuth: true,
      showInMenu: true,
      title: '物料 BOM',
    },
    name: 'materials',
    path: 'materials',
  },
  {
    component: () => import('@/pages/inventory/InventoryOverview.vue'),
    meta: {
      icon: 'inventory',
      isModule: true,
      module: 'inventory',
      pageOrder: 2,
      permission: PERMISSIONS.inventory.view,
      requiresAuth: true,
      showInMenu: true,
      title: '库存管理',
    },
    name: 'inventory',
    path: 'inventory',
  },
  {
    component: () => import('@/pages/inventory/CalcPage.vue'),
    meta: {
      icon: 'calc',
      module: 'inventory',
      pageOrder: 3,
      permission: PERMISSIONS.inventory.calc,
      requiresAuth: true,
      showInMenu: true,
      title: '物料缺口计算',
    },
    name: 'inventory-calc',
    path: 'inventory/calc',
  },
  {
    component: () => import('@/pages/inventory/MonitorPage.vue'),
    meta: {
      icon: 'monitor',
      module: 'inventory',
      pageOrder: 4,
      permission: PERMISSIONS.inventory.monitor,
      requiresAuth: true,
      showInMenu: true,
      title: '库存监控',
    },
    name: 'inventory-monitor',
    path: 'inventory/monitor',
  },
  {
    component: () => import('@/pages/inventory/RegisterPage.vue'),
    meta: {
      icon: 'register',
      module: 'inventory',
      pageOrder: 5,
      permission: PERMISSIONS.inventory.register,
      requiresAuth: true,
      showInMenu: true,
      title: '完工入库登记',
    },
    name: 'inventory-register',
    path: 'inventory/register',
  },
  {
    component: () => import('@/pages/purchase/PurchaseOverview.vue'),
    meta: {
      icon: 'purchase',
      isModule: true,
      module: 'purchase',
      pageOrder: 6,
      permission: PERMISSIONS.purchase.view,
      requiresAuth: true,
      showInMenu: true,
      title: '采购管理',
    },
    name: 'purchase',
    path: 'purchase',
  },
  {
    component: () => import('@/pages/production/ProductionOverview.vue'),
    meta: {
      icon: 'production',
      isModule: true,
      module: 'production',
      pageOrder: 7,
      permission: PERMISSIONS.production.view,
      requiresAuth: true,
      showInMenu: true,
      title: '生产管理',
    },
    name: 'production',
    path: 'production',
  },
  {
    component: () => import('@/pages/production/CapacityPage.vue'),
    meta: {
      icon: 'capacity',
      module: 'production',
      pageOrder: 8,
      permission: PERMISSIONS.production.capacity,
      requiresAuth: true,
      showInMenu: true,
      title: '产能配置',
    },
    name: 'production-capacity',
    path: 'production/capacity',
  },
  {
    component: () => import('@/pages/production/OrdersPage.vue'),
    meta: {
      icon: 'orders',
      module: 'production',
      pageOrder: 9,
      permission: PERMISSIONS.production.orders,
      requiresAuth: true,
      showInMenu: true,
      title: '生产订单',
    },
    name: 'production-orders',
    path: 'production/orders',
  },
  {
    component: () => import('@/pages/production/BreakdownPage.vue'),
    meta: {
      icon: 'breakdown',
      module: 'production',
      pageOrder: 10,
      permission: PERMISSIONS.production.breakdown,
      requiresAuth: true,
      showInMenu: true,
      title: '故障反馈',
    },
    name: 'production-breakdown',
    path: 'production/breakdown',
  },
  {
    component: () => import('@/pages/production/ProductionOperationsPage.vue'),
    meta: {
      icon: 'monitor',
      module: 'production',
      pageOrder: 11,
      permission: PERMISSIONS.production.view,
      requiresAuth: true,
      showInMenu: true,
      title: '生产运营',
    },
    name: 'production-operations',
    path: 'production/operations',
  },
  {
    component: () => import('@/pages/trace/TraceOverview.vue'),
    meta: {
      icon: 'trace',
      isModule: true,
      module: 'trace',
      pageOrder: 12,
      permission: PERMISSIONS.trace.view,
      requiresAuth: true,
      showInMenu: true,
      title: '质量追溯',
    },
    name: 'trace',
    path: 'trace',
  },
  {
    component: () => import('@/pages/system/SystemOverview.vue'),
    meta: {
      icon: 'system',
      isModule: true,
      module: 'system',
      pageOrder: 13,
      permission: PERMISSIONS.system.view,
      requiresAuth: true,
      showInMenu: true,
      title: '系统管理',
    },
    name: 'system',
    path: 'system',
  },
  {
    component: () => import('@/pages/system/UsersPage.vue'),
    meta: {
      icon: 'users',
      module: 'system',
      pageOrder: 14,
      permission: PERMISSIONS.system.userView,
      requiresAuth: true,
      showInMenu: true,
      title: '账号管理',
    },
    name: 'system-users',
    path: 'system/users',
  },
  {
    component: () => import('@/pages/system/AuditLogsPage.vue'),
    meta: {
      icon: 'audit',
      module: 'system',
      pageOrder: 16,
      permission: PERMISSIONS.system.auditView,
      requiresAuth: true,
      showInMenu: true,
      title: '操作审计',
    },
    name: 'system-audit-logs',
    path: 'system/audit-logs',
  },
  {
    component: () => import('@/pages/system/RolesPage.vue'),
    meta: {
      icon: 'roles',
      module: 'system',
      pageOrder: 15,
      permission: PERMISSIONS.system.roleView,
      requiresAuth: true,
      showInMenu: true,
      title: '角色管理',
    },
    name: 'system-roles',
    path: 'system/roles',
  },
  {
    component: () => import('@/pages/errors/ForbiddenPage.vue'),
    meta: { requiresAuth: true, title: '无权限访问' },
    name: 'forbidden',
    path: 'forbidden',
  },
]

export const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { children: adminRoutes, component: () => import('@/layouts/AdminLayout.vue'), path: '/' },
    {
      component: () => import('@/pages/errors/NotFoundPage.vue'),
      meta: { title: '页面不存在' },
      name: 'not-found',
      path: '/:pathMatch(.*)*',
    },
  ],
})

let initializedAccessToken: string | undefined = undefined

router.beforeEach(async (to) => {
  if (!to.meta.requiresAuth) {
    return true
  }

  const auth = useAuthStore(pinia)
  if (!auth.restoreSession()) {
    globalThis.location.assign(`/login.html?redirect=${encodeURIComponent(to.fullPath)}`)
    return false
  }

  // 每次页面加载后从后端恢复权限，避免沿用此前初始化失败时保存的空权限。
  if (initializedAccessToken !== auth.token) {
    try {
      const access = await systemService.loadCurrentAccess()
      auth.setCurrentUser(access.currentUser)
      auth.setRoles(access.roles)
      auth.setPermissions(access.permissions)
      initializedAccessToken = auth.token
    } catch {
      auth.logout(false)
      globalThis.location.assign(`/login.html?redirect=${encodeURIComponent(to.fullPath)}`)
      return false
    }
  }

  if (typeof to.meta.permission === 'string' && !auth.hasPermission(to.meta.permission)) {
    return { name: 'forbidden', replace: true }
  }

  return true
})
