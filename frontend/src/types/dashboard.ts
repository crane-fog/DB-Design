import type { PermissionCode } from '@/api'

export interface DashboardAccess {
  permissions: readonly PermissionCode[]
}

export type DashboardStatisticKey =
  | 'auditLogs'
  | 'inventoryAlerts'
  | 'permissions'
  | 'purchaseReminders'
  | 'roles'
  | 'users'

export interface DashboardStatistic {
  description: string
  key: DashboardStatisticKey
  permission: PermissionCode
  route: string
  title: string
  /** 未加载成功时不显示数字，不能用 0 代替未知总数。 */
  value?: number
}

export type DashboardShortcutIcon = 'audit' | 'materials' | 'roles' | 'users'

export interface DashboardShortcut {
  description: string
  icon: DashboardShortcutIcon
  permission: PermissionCode
  route: string
  title: string
}

export interface DashboardTodo {
  createdAt: string
  id: string
  permission: PermissionCode
  route: string
  statusLabel: string
  title: string
  type: 'reminder' | 'warning'
}

/** 仅保留操作日志接口提供的字段，不推断姓名或操作成功与否。 */
export interface DashboardOperation {
  action?: string
  id?: number
  ipAddress?: string
  module?: string
  operateTime?: string
  operatorId?: number
}

export interface DashboardSection<TItem> {
  errors: string[]
  items: TItem[]
  state: 'error' | 'forbidden' | 'partial' | 'ready'
  /** 仅在所有有权访问的数据源均成功时提供完整总数。 */
  total?: number
}

export interface SystemDashboardData {
  errors: string[]
  recentOperations: DashboardSection<DashboardOperation>
  statistics: DashboardStatistic[]
}

export interface HomeDashboardData extends SystemDashboardData {
  todos: DashboardSection<DashboardTodo>
}
