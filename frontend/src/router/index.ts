import { PermissionCode, type PermissionCodeValue } from '@/constants/permissions'
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
    permissions?: readonly PermissionCodeValue[]
    requiresAuth?: boolean
    showInMenu?: boolean
    showOverviewInMenu?: boolean
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
    meta: {
      icon: 'materials',
      isModule: true,
      module: 'materials',
      pageOrder: 1,
      permissions: [
        PermissionCode.MaterialItemView,
        PermissionCode.MaterialBomVersionView,
        PermissionCode.MaterialBomView,
        PermissionCode.MaterialBomTreeView,
        PermissionCode.MaterialBomReverseView,
        PermissionCode.MaterialCostCalculate,
        PermissionCode.MaterialLossCalculate,
      ],
      requiresAuth: true,
      showInMenu: true,
      title: '物料管理',
    },
    name: 'materials',
    path: 'materials',
    redirect: { name: 'materials-master-data' },
  },
  {
    component: () => import('@/pages/materials/MaterialMasterPage.vue'),
    meta: {
      module: 'materials',
      pageOrder: 2,
      permissions: [PermissionCode.MaterialItemView],
      requiresAuth: true,
      showInMenu: true,
      title: '物料主数据',
    },
    name: 'materials-master-data',
    path: 'materials/master-data',
  },
  {
    component: () => import('@/pages/materials/BomMaintenancePage.vue'),
    meta: {
      module: 'materials',
      pageOrder: 3,
      permissions: [PermissionCode.MaterialBomVersionView, PermissionCode.MaterialBomView],
      requiresAuth: true,
      showInMenu: true,
      title: 'BOM 维护与版本',
    },
    name: 'materials-bom-maintenance',
    path: 'materials/bom-maintenance',
  },
  {
    component: () => import('@/pages/materials/BomTreePage.vue'),
    meta: {
      module: 'materials',
      pageOrder: 4,
      permissions: [PermissionCode.MaterialBomTreeView],
      requiresAuth: true,
      showInMenu: true,
      title: 'BOM 结构树',
    },
    name: 'materials-bom-tree',
    path: 'materials/bom-tree',
  },
  {
    component: () => import('@/pages/materials/MaterialAnalysisPage.vue'),
    meta: {
      module: 'materials',
      pageOrder: 5,
      permissions: [PermissionCode.MaterialCostCalculate, PermissionCode.MaterialLossCalculate],
      requiresAuth: true,
      showInMenu: true,
      title: '用料分析',
    },
    name: 'materials-analysis',
    path: 'materials/analysis',
  },
  {
    component: () => import('@/pages/materials/BomReverseTracePage.vue'),
    meta: {
      module: 'materials',
      pageOrder: 6,
      permissions: [PermissionCode.MaterialBomReverseView],
      requiresAuth: true,
      showInMenu: true,
      title: 'BOM 反向追溯',
    },
    name: 'materials-bom-reverse-trace',
    path: 'materials/bom-reverse-trace',
  },
  {
    component: () => import('@/pages/inventory/InventoryOverview.vue'),
    meta: {
      icon: 'inventory',
      isModule: true,
      module: 'inventory',
      pageOrder: 2,
      permissions: [PermissionCode.InventoryStockView],
      requiresAuth: true,
      showInMenu: true,
      showOverviewInMenu: true,
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
      permissions: [PermissionCode.InventoryShortageCalculate],
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
      permissions: [
        PermissionCode.InventoryAlertView,
        PermissionCode.InventoryLockView,
        PermissionCode.InventoryObsoleteView,
      ],
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
      permissions: [PermissionCode.InventoryCompletionView],
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
      permissions: [
        PermissionCode.PurchaseOrderView,
        PermissionCode.PurchaseReceiptView,
        PermissionCode.PurchaseOverdueView,
      ],
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
      permissions: [
        PermissionCode.ProductionOrderView,
        PermissionCode.ProductionLineView,
        PermissionCode.ProductionLineTypeView,
        PermissionCode.ProductionCapacityConfigView,
        PermissionCode.ProductionCalendarView,
        PermissionCode.ProductionFaultView,
        PermissionCode.ExternalOrderViewOwn,
        PermissionCode.ExternalOrderViewAll,
      ],
      requiresAuth: true,
      showInMenu: true,
      showOverviewInMenu: true,
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
      permissions: [
        PermissionCode.ProductionLineView,
        PermissionCode.ProductionLineTypeView,
        PermissionCode.ProductionCapacityConfigView,
        PermissionCode.ProductionCalendarView,
      ],
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
      permissions: [PermissionCode.ProductionOrderView],
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
      permissions: [
        PermissionCode.ProductionFaultView,
        PermissionCode.ProductionFaultReport,
        PermissionCode.ProductionFaultClaim,
        PermissionCode.ProductionFaultUpdateAssigned,
        PermissionCode.ProductionFaultUpdateAny,
      ],
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
      permissions: [
        PermissionCode.ExternalOrderViewOwn,
        PermissionCode.ExternalOrderViewAll,
        PermissionCode.ExternalOrderCreateOwn,
        PermissionCode.ExternalOrderCreateForCustomer,
        PermissionCode.ProductionCapacityEstimate,
        PermissionCode.ProductionCapacityDetect,
        PermissionCode.ProductionCapacityBalance,
        PermissionCode.ProductionLineStatusUpdate,
      ],
      requiresAuth: true,
      showInMenu: true,
      title: '生产运营',
    },
    name: 'production-operations',
    path: 'production/operations',
  },
  {
    meta: {
      icon: 'trace',
      isModule: true,
      module: 'trace',
      pageOrder: 12,
      permissions: [
        PermissionCode.TraceConsumptionView,
        PermissionCode.TraceProductView,
        PermissionCode.TraceMaterialView,
      ],
      requiresAuth: true,
      showInMenu: true,
      title: '质量追溯',
    },
    name: 'trace',
    path: 'trace',
    redirect: { name: 'trace-consumption' },
  },
  {
    component: () => import('@/pages/trace/TraceConsumptionPage.vue'),
    meta: {
      module: 'trace',
      pageOrder: 13,
      permissions: [PermissionCode.TraceConsumptionView],
      requiresAuth: true,
      showInMenu: true,
      title: '批次消耗',
    },
    name: 'trace-consumption',
    path: 'trace/consumption',
  },
  {
    component: () => import('@/pages/trace/ProductTracePage.vue'),
    meta: {
      module: 'trace',
      pageOrder: 14,
      permissions: [PermissionCode.TraceProductView],
      requiresAuth: true,
      showInMenu: true,
      title: '正向追溯',
    },
    name: 'trace-product',
    path: 'trace/product',
  },
  {
    component: () => import('@/pages/trace/MaterialTracePage.vue'),
    meta: {
      module: 'trace',
      pageOrder: 15,
      permissions: [PermissionCode.TraceMaterialView],
      requiresAuth: true,
      showInMenu: true,
      title: '反向追溯',
    },
    name: 'trace-material',
    path: 'trace/material',
  },
  {
    component: () => import('@/pages/system/SystemOverview.vue'),
    meta: {
      icon: 'system',
      isModule: true,
      module: 'system',
      pageOrder: 13,
      permissions: [
        PermissionCode.SystemUserView,
        PermissionCode.SystemRoleView,
        PermissionCode.SystemPermissionView,
        PermissionCode.SystemAuditLoginView,
        PermissionCode.SystemAuditOperationView,
      ],
      requiresAuth: true,
      showInMenu: true,
      showOverviewInMenu: true,
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
      permissions: [PermissionCode.SystemUserView],
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
      permissions: [PermissionCode.SystemAuditLoginView, PermissionCode.SystemAuditOperationView],
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
      permissions: [PermissionCode.SystemRoleView],
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
      auth.setAccess(access)
      initializedAccessToken = auth.token
    } catch {
      auth.logout(false)
      globalThis.location.assign(`/login.html?redirect=${encodeURIComponent(to.fullPath)}`)
      return false
    }
  }

  if (to.meta.permissions?.length && !auth.hasAnyPermission(...to.meta.permissions)) {
    return { name: 'forbidden', replace: true }
  }

  return true
})
