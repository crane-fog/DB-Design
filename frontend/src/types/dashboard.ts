export type DashboardMockScenario = 'empty' | 'error' | 'success'

export type DashboardStatisticKey =
  | 'auditLogs'
  | 'pendingItems'
  | 'roles'
  | 'todayOperations'
  | 'users'

export interface DashboardStatistic {
  description: string
  key: DashboardStatisticKey
  permission?: string
  route?: string
  title: string
  value: number
}

export type DashboardShortcutIcon = 'audit' | 'materials' | 'roles' | 'users'

export interface DashboardShortcut {
  description: string
  icon: DashboardShortcutIcon
  permission: string
  route: string
  title: string
}

export type DashboardTodoStatus = 'pending' | 'processing' | 'resolved'
export type DashboardTodoType = 'notice' | 'reminder' | 'warning'

export interface DashboardTodo {
  createdAt: string
  id: string
  permission?: string
  route?: string
  status: DashboardTodoStatus
  title: string
  type: DashboardTodoType
}

export type DashboardOperationResult = 'failure' | 'success'

/** 与系统审计操作日志保持相同的展示字段，供概览页精简呈现。 */
export interface DashboardOperation {
  action: string
  id: string
  ipAddress?: string
  module: string
  operateTime: string
  operatorName: string
  permission?: string
  result: DashboardOperationResult
}

export interface PageResult<TEntity> {
  items: TEntity[]
  page: number
  pageSize: number
  total: number
}

export interface HomeDashboardData {
  recentOperations: PageResult<DashboardOperation>
  shortcuts: DashboardShortcut[]
  statistics: DashboardStatistic[]
  todos: PageResult<DashboardTodo>
}

export interface SystemDashboardData {
  recentOperations: PageResult<DashboardOperation>
  shortcuts: DashboardShortcut[]
  statistics: DashboardStatistic[]
}
