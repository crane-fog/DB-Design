import { type ApiEnvelope, unwrap } from '@/services/pagination'
import type {
  DashboardAccess,
  DashboardOperation,
  DashboardSection,
  DashboardShortcut,
  DashboardStatistic,
  DashboardTodo,
  HomeDashboardData,
  SystemDashboardData,
} from '@/types/dashboard'
import type {
  InventoryAlertEvent,
  OperationLog,
  PermissionCode as PermissionCodeValue,
  PurchaseOverdueReminder,
} from '@/api'
import { inventoryApi, purchaseApi, systemApi } from '@/api/client'
import { PermissionCode } from '@/constants/permissions'
import { getErrorMessage } from '@/utils/error'

// 导航是静态配置，不依赖任何业务接口是否加载成功。
export const systemDashboardShortcuts: readonly DashboardShortcut[] = [
  {
    description: '维护账号状态和角色分配',
    icon: 'users',
    permission: PermissionCode.SystemUserView,
    route: '/system/users',
    title: '用户管理',
  },
  {
    description: '维护角色及其权限范围',
    icon: 'roles',
    permission: PermissionCode.SystemRoleView,
    route: '/system/roles',
    title: '角色管理',
  },
  {
    description: '查询登录与操作审计记录',
    icon: 'audit',
    permission: PermissionCode.SystemAuditOperationView,
    route: '/system/audit-logs',
    title: '审计日志',
  },
]

export const homeDashboardShortcuts: readonly DashboardShortcut[] = [
  ...systemDashboardShortcuts,
  {
    description: '查看物料 BOM 版本和组件用量',
    icon: 'materials',
    permission: PermissionCode.MaterialItemView,
    route: '/materials',
    title: '物料管理',
  },
]

export function hasDashboardPermission(access: DashboardAccess, permission: PermissionCodeValue) {
  return access.permissions.includes(permission)
}

const previewQuery = { page: 1, pageSize: 5 }
const countQuery = { page: 1, pageSize: 1 }
type PageRequest = () => Promise<{ data: ApiEnvelope<unknown> }>

async function loadSection<TSource, TItem = TSource>(
  access: DashboardAccess,
  source: {
    map: (item: TSource) => TItem
    permission: PermissionCodeValue
    request: PageRequest
    title: string
  },
): Promise<DashboardSection<TItem>> {
  const { map, permission, request, title } = source
  if (!hasDashboardPermission(access, permission)) {
    return { errors: [], items: [], state: 'forbidden' }
  }
  try {
    const response = await request()
    const payload = unwrap(response.data)
    if (response.data.code !== 200) {
      throw new Error('接口未返回成功状态')
    }
    // 这些列表接口均返回 records/total；缺失或异常响应不能当作空列表和零总数。
    if (!payload || typeof payload !== 'object') {
      throw new Error('接口未返回分页数据')
    }
    const page = payload as { records?: TSource[]; total?: number }
    if (
      !Array.isArray(page.records) ||
      typeof page.total !== 'number' ||
      !Number.isSafeInteger(page.total) ||
      page.total < page.records.length
    ) {
      throw new Error('接口返回的分页数据不完整')
    }
    return { errors: [], items: page.records.map(map), state: 'ready', total: page.total }
  } catch (error) {
    return {
      errors: [`${title}：${getErrorMessage(error, '加载失败，请重试')}`],
      items: [],
      state: 'error',
    }
  }
}

function statistic(
  source: DashboardSection<unknown>,
  metadata: Omit<DashboardStatistic, 'value'>,
): DashboardStatistic[] {
  if (source.state === 'forbidden') {
    return []
  }
  return [{ ...metadata, value: source.total }]
}

async function loadSystemData(
  access: DashboardAccess,
  includePermissions: boolean,
): Promise<SystemDashboardData> {
  let permissionRequest = Promise.resolve<DashboardSection<unknown>>({
    errors: [],
    items: [],
    state: 'forbidden',
  })
  if (includePermissions) {
    permissionRequest = loadSection(access, {
      map: (item) => item,
      permission: PermissionCode.SystemPermissionView,
      request: () => systemApi.listPermissionData(countQuery),
      title: '权限统计',
    })
  }
  const [users, roles, operations, permissions] = await Promise.all([
    loadSection(access, {
      map: (item) => item,
      permission: PermissionCode.SystemUserView,
      request: () => systemApi.listUserData(countQuery),
      title: '用户统计',
    }),
    loadSection(access, {
      map: (item) => item,
      permission: PermissionCode.SystemRoleView,
      request: () => systemApi.listRoleData(countQuery),
      title: '角色统计',
    }),
    loadSection<OperationLog, DashboardOperation>(access, {
      map: (log) => ({
        action: log.action,
        id: log.log_id,
        ipAddress: log.ip_address,
        module: log.module,
        operateTime: log.operate_time,
        operatorId: log.operator_id,
      }),
      permission: PermissionCode.SystemAuditOperationView,
      request: () => systemApi.listOperationLogData(previewQuery),
      title: '操作日志',
    }),
    permissionRequest,
  ])

  return {
    errors: [...users.errors, ...roles.errors, ...permissions.errors, ...operations.errors],
    recentOperations: operations,
    statistics: [
      ...statistic(users, {
        description: '已登记账号，包含启用和停用账号',
        key: 'users',
        permission: PermissionCode.SystemUserView,
        route: '/system/users',
        title: '用户总数',
      }),
      ...statistic(roles, {
        description: '已配置角色，包含启用和停用角色',
        key: 'roles',
        permission: PermissionCode.SystemRoleView,
        route: '/system/roles',
        title: '角色总数',
      }),
      ...statistic(permissions, {
        description: '系统登记的权限条目',
        key: 'permissions',
        permission: PermissionCode.SystemPermissionView,
        route: '/system/roles',
        title: '权限数量',
      }),
      ...statistic(operations, {
        description: '已记录的操作日志，不含登录日志',
        key: 'auditLogs',
        permission: PermissionCode.SystemAuditOperationView,
        route: '/system/audit-logs',
        title: '操作日志总数',
      }),
    ],
  }
}

/** 仅聚合现有只读列表接口；不生成预警/催单，不请求无权访问的用户目录。 */
export const dashboardService = {
  async getHomeDashboard(access: DashboardAccess): Promise<HomeDashboardData> {
    const [system, alerts, reminders] = await Promise.all([
      loadSystemData(access, false),
      loadSection<InventoryAlertEvent, DashboardTodo>(access, {
        map: (alert) => ({
          createdAt: alert.alert_time,
          id: `inventory-alert-${alert.alert_id}`,
          permission: PermissionCode.InventoryAlertView,
          route: '/inventory/monitor',
          statusLabel: { handled: '已处理', ignored: '已忽略', pending: '待处理' }[alert.status],
          title: `库存预警：${alert.material_name || `物料 #${alert.material_id}`}（预警 #${alert.alert_id}）`,
          type: 'warning',
        }),
        permission: PermissionCode.InventoryAlertView,
        request: () => inventoryApi.listInventoryAlert({ ...previewQuery, status: 'pending' }),
        title: '库存待处理预警',
      }),
      loadSection<PurchaseOverdueReminder, DashboardTodo>(access, {
        map: (reminder) => ({
          createdAt: reminder.remind_time,
          id: `purchase-reminder-${reminder.reminder_id}`,
          permission: PermissionCode.PurchaseOverdueView,
          route: '/purchase',
          statusLabel: { pending_urge: '待催交', received: '已到货', urged: '已催交' }[
            reminder.status
          ],
          title: `采购订单 #${reminder.order_id} 逾期 ${reminder.overdue_days} 天`,
          type: 'reminder',
        }),
        permission: PermissionCode.PurchaseOverdueView,
        request: () =>
          purchaseApi.listPurchaseOverdueReminder({ ...previewQuery, status: 'pending_urge' }),
        title: '采购待催交提醒',
      }),
    ])
    const sources = [alerts, reminders].filter((source) => source.state !== 'forbidden')
    const ready = sources.filter((source) => source.state === 'ready')
    const errors = sources.flatMap((source) => source.errors)
    const items = sources.flatMap((source) => source.items)
    items.sort((left, right) => Date.parse(right.createdAt) - Date.parse(left.createdAt))
    let state: DashboardSection<DashboardTodo>['state'] = 'forbidden'
    let total: number | undefined = undefined
    if (sources.length) {
      if (ready.length === sources.length) {
        state = 'ready'
        total = ready.reduce((sum, source) => sum + (source.total as number), 0)
      } else if (ready.length) {
        state = 'partial'
      } else {
        state = 'error'
      }
    }
    const todos: DashboardSection<DashboardTodo> = {
      errors,
      // 各源取接口首页的五条记录，在新数组中按时间排序，不声称是全部待办。
      items,
      state,
      total,
    }
    return {
      ...system,
      errors: [...system.errors, ...errors],
      statistics: [
        ...system.statistics,
        ...statistic(alerts, {
          description: '已生成且尚未处理的库存预警',
          key: 'inventoryAlerts',
          permission: PermissionCode.InventoryAlertView,
          route: '/inventory/monitor',
          title: '待处理库存预警',
        }),
        ...statistic(reminders, {
          description: '已生成且状态为待催交的采购提醒',
          key: 'purchaseReminders',
          permission: PermissionCode.PurchaseOverdueView,
          route: '/purchase',
          title: '待催交采购提醒',
        }),
      ],
      todos,
    }
  },
  getSystemDashboard(access: DashboardAccess): Promise<SystemDashboardData> {
    return loadSystemData(access, true)
  },
}
