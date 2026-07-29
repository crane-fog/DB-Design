import type {
  DashboardMockScenario,
  DashboardOperation,
  DashboardShortcut,
  DashboardStatistic,
  DashboardTodo,
  HomeDashboardData,
  PageResult,
  SystemDashboardData,
} from '@/types/dashboard'

const userStatistic: DashboardStatistic = {
  description: '当前启用账号总数',
  key: 'users',
  permission: 'system:user:view',
  route: '/system/users',
  title: '用户总数',
  value: 128,
}

const roleStatistic: DashboardStatistic = {
  description: '已配置业务角色',
  key: 'roles',
  permission: 'system:role:view',
  route: '/system/roles',
  title: '角色总数',
  value: 6,
}

const statistics: DashboardStatistic[] = [
  userStatistic,
  roleStatistic,
  {
    description: '需要关注的系统事项',
    key: 'pendingItems',
    title: '待处理事项',
    value: 3,
  },
  {
    description: '截至当前时刻的系统记录',
    key: 'todayOperations',
    permission: 'system:audit:view',
    route: '/system/audit-logs',
    title: '今日操作次数',
    value: 42,
  },
]

const systemStatistics: DashboardStatistic[] = [
  userStatistic,
  roleStatistic,
  {
    description: '已登记的细粒度权限标识',
    key: 'auditLogs',
    permission: 'system:role:view',
    route: '/system/roles',
    title: '权限数量',
    value: 20,
  },
  {
    description: '近 7 天的审计记录',
    key: 'auditLogs',
    permission: 'system:audit:view',
    route: '/system/audit-logs',
    title: '近期审计日志',
    value: 86,
  },
]

const shortcuts: DashboardShortcut[] = [
  {
    description: '维护账号状态和角色分配',
    icon: 'users',
    permission: 'system:user:view',
    route: '/system/users',
    title: '用户管理',
  },
  {
    description: '维护角色及其权限范围',
    icon: 'roles',
    permission: 'system:role:view',
    route: '/system/roles',
    title: '角色管理',
  },
  {
    description: '查询登录与操作审计记录',
    icon: 'audit',
    permission: 'system:audit:view',
    route: '/system/audit-logs',
    title: '审计日志',
  },
  {
    description: '查看物料 BOM 版本和组件用量',
    icon: 'materials',
    permission: 'material:view',
    route: '/materials',
    title: '物料 BOM',
  },
]

const todos: DashboardTodo[] = [
  {
    createdAt: '2026-07-17T09:20:00',
    id: 'todo-001',
    permission: 'system:user:view',
    route: '/system/users',
    status: 'pending',
    title: '请复核本周新增账号的角色分配',
    type: 'reminder',
  },
  {
    createdAt: '2026-07-17T08:45:00',
    id: 'todo-002',
    permission: 'inventory:monitor',
    route: '/inventory/monitor',
    status: 'processing',
    title: '库存监控中有 2 项物料低于安全库存',
    type: 'warning',
  },
  {
    createdAt: '2026-07-16T16:30:00',
    id: 'todo-003',
    status: 'resolved',
    title: '系统权限配置已完成例行检查',
    type: 'notice',
  },
]

const operations: DashboardOperation[] = [
  {
    action: '更新用户状态',
    id: 'operation-001',
    ipAddress: '10.10.2.15',
    module: '用户管理',
    operateTime: '2026-07-17T10:18:00',
    operatorName: '系统管理员',
    permission: 'system:user:view',
    result: 'success',
  },
  {
    action: '调整角色权限',
    id: 'operation-002',
    ipAddress: '10.10.2.15',
    module: '角色管理',
    operateTime: '2026-07-17T09:42:00',
    operatorName: '系统管理员',
    permission: 'system:role:view',
    result: 'success',
  },
  {
    action: '登录系统',
    id: 'operation-003',
    ipAddress: '10.10.1.24',
    module: '登录认证',
    operateTime: '2026-07-17T09:10:00',
    operatorName: 'GD0001',
    permission: 'system:audit:view',
    result: 'success',
  },
  {
    action: '导出操作日志',
    id: 'operation-004',
    ipAddress: '10.10.2.18',
    module: '系统审计',
    operateTime: '2026-07-16T17:26:00',
    operatorName: '系统管理员',
    permission: 'system:audit:view',
    result: 'success',
  },
]

function clonePage<TEntity>(items: TEntity[], pageSize: number): PageResult<TEntity> {
  return { items: [...items].slice(0, pageSize), page: 1, pageSize, total: items.length }
}

function cloneHomeData(scenario: DashboardMockScenario): HomeDashboardData {
  const isEmpty = scenario === 'empty'
  let recentOperations = operations
  let todoItems = todos
  if (isEmpty) {
    recentOperations = []
    todoItems = []
  }
  return {
    recentOperations: clonePage(recentOperations, 5),
    shortcuts: [...shortcuts],
    statistics: [...statistics],
    todos: clonePage(todoItems, 5),
  }
}

function cloneSystemData(scenario: DashboardMockScenario): SystemDashboardData {
  const isEmpty = scenario === 'empty'
  let recentOperations = operations.slice(0, 3)
  if (isEmpty) {
    recentOperations = []
  }
  return {
    recentOperations: clonePage(recentOperations, 3),
    shortcuts: shortcuts.slice(0, 3),
    statistics: [...systemStatistics],
  }
}

function delay<TValue>(factory: () => TValue, scenario: DashboardMockScenario): Promise<TValue> {
  return new Promise((resolve, reject) => {
    globalThis.setTimeout(() => {
      if (scenario === 'error') {
        reject(new Error('工作台数据加载失败，请稍后重试'))
        return
      }
      resolve(factory())
    }, 180)
  })
}

export const dashboardMock = {
  getHomeDashboard(scenario: DashboardMockScenario) {
    return delay(() => cloneHomeData(scenario), scenario)
  },
  getSystemDashboard(scenario: DashboardMockScenario) {
    return delay(() => cloneSystemData(scenario), scenario)
  },
}

export function getHomeDashboardSeed() {
  return cloneHomeData('success')
}

export function getSystemDashboardSeed() {
  return cloneSystemData('success')
}
