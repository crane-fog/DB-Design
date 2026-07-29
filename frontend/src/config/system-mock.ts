import type {
  AccountStatus,
  LoginLogQuery,
  OperationLogQuery,
  PermissionTreeNode,
  RoleFormData,
  RolePermissionAssignment,
  RoleQuery,
  SystemLoginLog,
  SystemOperationLog,
  SystemRole,
  SystemRoleSummary,
  SystemUser,
  UserCreateFormData,
  UserFormData,
  UserQuery,
} from '@/services/SystemService'
import type { PageResult } from '@/services/pagination'

type MockScenario = 'empty' | 'error' | 'success'

interface MockUserRole {
  roleId: number
  userId: number
}

interface MockRolePermission {
  permissionId: number
  roleId: number
}

interface MockPermission {
  action: string
  id: number
  resource: string
}

interface MockLoginLog extends SystemLoginLog {
  userId: number
}

interface MockOperationLog extends SystemOperationLog {
  operatorId: number
}

let users: SystemUser[] = [
  {
    createdTime: '2026-01-08T09:00:00',
    email: 'admin@example.test',
    employeeNo: 'GD0001',
    id: 1001,
    lastLoginTime: '2026-07-29T09:10:00',
    name: '系统管理员',
    phone: '13800000001',
    pwdUpdateTime: '2026-01-08T09:00:00',
    status: 'valid',
  },
  {
    createdTime: '2026-01-12T10:30:00',
    email: 'dev.admin@example.test',
    employeeNo: 'DEV_ADMIN',
    id: 1002,
    lastLoginTime: '2026-07-29T09:00:00',
    name: '本地开发管理员',
    phone: '13800000002',
    pwdUpdateTime: '2026-01-12T10:30:00',
    status: 'valid',
  },
  {
    createdTime: '2026-01-14T14:00:00',
    email: 'dev.user@example.test',
    employeeNo: 'DEV_USER',
    id: 1003,
    lastLoginTime: '2026-07-28T16:20:00',
    name: '本地开发用户',
    phone: '13800000003',
    pwdUpdateTime: '2026-01-14T14:00:00',
    status: 'valid',
  },
  {
    createdTime: '2026-02-03T09:15:00',
    email: 'warehouse@example.test',
    employeeNo: 'WH0012',
    id: 1004,
    lastLoginTime: '2026-07-28T15:40:00',
    name: '仓储主管',
    phone: '13800000012',
    pwdUpdateTime: '2026-02-03T09:15:00',
    status: 'valid',
  },
  {
    createdTime: '2026-02-18T11:20:00',
    email: 'buyer@example.test',
    employeeNo: 'PO0021',
    id: 1005,
    lastLoginTime: '2026-07-28T14:30:00',
    name: '采购专员',
    phone: '13800000021',
    pwdUpdateTime: '2026-02-18T11:20:00',
    status: 'valid',
  },
  {
    createdTime: '2026-03-02T08:45:00',
    email: 'production@example.test',
    employeeNo: 'PR0033',
    id: 1006,
    lastLoginTime: '2026-07-27T18:00:00',
    name: '生产计划员',
    phone: '13800000033',
    pwdUpdateTime: '2026-03-02T08:45:00',
    status: 'valid',
  },
  {
    createdTime: '2026-03-10T13:10:00',
    email: 'auditor@example.test',
    employeeNo: 'AU0044',
    id: 1007,
    lastLoginTime: '2026-07-26T10:10:00',
    name: '审计人员',
    phone: '13800000044',
    pwdUpdateTime: '2026-03-10T13:10:00',
    status: 'valid',
  },
  {
    createdTime: '2026-03-15T16:00:00',
    email: 'former@example.test',
    employeeNo: 'OLD0055',
    id: 1008,
    lastLoginTime: '2026-04-01T09:00:00',
    name: '停用账号',
    phone: '13800000055',
    pwdUpdateTime: '2026-03-15T16:00:00',
    status: 'disabled',
  },
  ...Array.from({ length: 5 }, (_index, index) => ({
    createdTime: '2026-04-01T09:00:00',
    email: `operator${index + 1}@example.test`,
    employeeNo: `OP00${index + 6}`,
    id: 1009 + index,
    lastLoginTime: '2026-07-25T09:00:00',
    name: `业务用户${index + 1}`,
    phone: `138000000${60 + index}`,
    pwdUpdateTime: '2026-04-01T09:00:00',
    status: 'valid' as const,
  })),
]

let roles: SystemRole[] = [
  { description: '拥有系统全部管理权限', id: 1, name: '系统管理员', status: 'valid' },
  { description: '负责用户与角色维护', id: 2, name: '系统运维员', status: 'valid' },
  { description: '负责仓储业务处理', id: 3, name: '仓储主管', status: 'valid' },
  { description: '负责采购业务处理', id: 4, name: '采购专员', status: 'valid' },
  { description: '只读查看审计信息', id: 5, name: '审计人员', status: 'valid' },
  { description: '历史角色，不再使用', id: 6, name: '停用角色', status: 'disabled' },
  ...Array.from({ length: 6 }, (_index, index) => ({
    description: '业务只读角色',
    id: 7 + index,
    name: `业务角色${index + 1}`,
    status: 'valid' as const,
  })),
]

const permissions: MockPermission[] = [
  { action: '查看', id: 1, resource: '系统管理' },
  { action: '查看用户', id: 2, resource: '用户管理' },
  { action: '创建用户', id: 3, resource: '用户管理' },
  { action: '修改用户', id: 4, resource: '用户管理' },
  { action: '查看角色', id: 5, resource: '角色管理' },
  { action: '创建角色', id: 6, resource: '角色管理' },
  { action: '修改角色', id: 7, resource: '角色管理' },
  { action: '分配权限', id: 8, resource: '角色管理' },
  { action: '查看审计日志', id: 9, resource: '审计日志' },
  { action: '查看物料', id: 10, resource: '物料管理' },
  { action: '查看库存', id: 11, resource: '库存管理' },
  { action: '查看采购', id: 12, resource: '采购管理' },
  { action: '查看生产', id: 13, resource: '生产管理' },
  { action: '查看追溯', id: 14, resource: '质量追溯' },
]

let userRoles: MockUserRole[] = [
  { roleId: 1, userId: 1001 },
  { roleId: 1, userId: 1002 },
  { roleId: 2, userId: 1007 },
  { roleId: 3, userId: 1004 },
  { roleId: 4, userId: 1005 },
  { roleId: 5, userId: 1007 },
  { roleId: 6, userId: 1008 },
]

let rolePermissions: MockRolePermission[] = [
  ...permissions.map(({ id: permissionId }) => ({ permissionId, roleId: 1 })),
  { permissionId: 1, roleId: 2 },
  { permissionId: 2, roleId: 2 },
  { permissionId: 4, roleId: 2 },
  { permissionId: 5, roleId: 2 },
  { permissionId: 7, roleId: 2 },
  { permissionId: 8, roleId: 2 },
  { permissionId: 11, roleId: 3 },
  { permissionId: 12, roleId: 4 },
  { permissionId: 9, roleId: 5 },
]

const loginLogs: MockLoginLog[] = [
  {
    id: 2001,
    ipAddress: '10.10.1.10',
    loginTime: '2026-07-29T09:10:00',
    result: 'success',
    userId: 1001,
  },
  {
    failReason: '密码错误',
    id: 2002,
    ipAddress: '10.10.1.22',
    loginTime: '2026-07-29T08:55:00',
    result: 'failure',
    userId: 1005,
  },
  {
    id: 2003,
    ipAddress: '10.10.2.15',
    loginTime: '2026-07-29T09:00:00',
    result: 'success',
    userId: 1002,
  },
  {
    id: 2004,
    ipAddress: '10.10.3.12',
    loginTime: '2026-07-28T16:20:00',
    result: 'success',
    userId: 1003,
  },
  {
    failReason: '账号已停用',
    id: 2005,
    ipAddress: '10.10.4.20',
    loginTime: '2026-07-28T15:00:00',
    result: 'failure',
    userId: 1008,
  },
  {
    id: 2006,
    ipAddress: '10.10.5.18',
    loginTime: '2026-07-28T14:30:00',
    result: 'success',
    userId: 1005,
  },
  {
    id: 2007,
    ipAddress: '10.10.6.21',
    loginTime: '2026-07-27T18:00:00',
    result: 'success',
    userId: 1006,
  },
  {
    id: 2008,
    ipAddress: '10.10.7.13',
    loginTime: '2026-07-27T10:10:00',
    result: 'success',
    userId: 1007,
  },
  {
    id: 2009,
    ipAddress: '10.10.8.11',
    loginTime: '2026-07-26T09:30:00',
    result: 'success',
    userId: 1004,
  },
  {
    failReason: '账号不存在',
    id: 2010,
    ipAddress: '10.10.9.19',
    loginTime: '2026-07-25T11:05:00',
    result: 'failure',
    userId: 1008,
  },
  {
    id: 2011,
    ipAddress: '10.10.1.10',
    loginTime: '2026-07-24T09:15:00',
    result: 'success',
    userId: 1001,
  },
]

const operationLogs: MockOperationLog[] = [
  {
    action: '更新用户状态',
    afterData: '{"status":"disabled"}',
    beforeData: '{"status":"valid"}',
    id: 3001,
    ipAddress: '10.10.1.10',
    module: '用户管理',
    operateTime: '2026-07-29T09:20:00',
    operatorId: 1001,
  },
  {
    action: '调整角色权限',
    afterData: '{"permissionCount":14}',
    beforeData: '{"permissionCount":9}',
    id: 3002,
    ipAddress: '10.10.1.10',
    module: '角色管理',
    operateTime: '2026-07-29T09:18:00',
    operatorId: 1001,
  },
  {
    action: '登录系统',
    afterData: '{"result":"success"}',
    id: 3003,
    ipAddress: '10.10.2.15',
    module: '登录认证',
    operateTime: '2026-07-29T09:00:00',
    operatorId: 1002,
  },
  {
    action: '查看审计日志',
    id: 3004,
    ipAddress: '10.10.7.13',
    module: '审计日志',
    operateTime: '2026-07-27T10:12:00',
    operatorId: 1007,
  },
  {
    action: '更新采购订单',
    afterData: '{"status":"submitted"}',
    beforeData: '{"status":"draft"}',
    id: 3005,
    ipAddress: '10.10.5.18',
    module: '采购管理',
    operateTime: '2026-07-28T14:45:00',
    operatorId: 1005,
  },
  {
    action: '完成库存预警',
    afterData: '{"status":"handled"}',
    beforeData: '{"status":"pending"}',
    id: 3006,
    ipAddress: '10.10.8.11',
    module: '库存管理',
    operateTime: '2026-07-26T09:45:00',
    operatorId: 1004,
  },
  {
    action: '新增生产订单',
    afterData: '{"status":"pending_review"}',
    id: 3007,
    ipAddress: '10.10.6.21',
    module: '生产管理',
    operateTime: '2026-07-27T18:10:00',
    operatorId: 1006,
  },
  {
    action: '查看质量追溯',
    id: 3008,
    ipAddress: '10.10.3.12',
    module: '质量追溯',
    operateTime: '2026-07-28T16:30:00',
    operatorId: 1003,
  },
  {
    action: '修改用户信息',
    afterData: '{"phone":"13800000003"}',
    beforeData: '{"phone":"13800000000"}',
    id: 3009,
    ipAddress: '10.10.2.15',
    module: '用户管理',
    operateTime: '2026-07-28T16:25:00',
    operatorId: 1002,
  },
  {
    action: '查看角色列表',
    id: 3010,
    ipAddress: '10.10.7.13',
    module: '角色管理',
    operateTime: '2026-07-26T10:20:00',
    operatorId: 1007,
  },
  {
    action: '更新排产日历',
    afterData: '{"date":"2026-07-30"}',
    beforeData: '{"date":"2026-07-29"}',
    id: 3011,
    ipAddress: '10.10.6.21',
    module: '生产管理',
    operateTime: '2026-07-25T17:40:00',
    operatorId: 1006,
  },
]

let nextUserId = Math.max(...users.map((user) => user.id)) + 1
let nextRoleId = Math.max(...roles.map((role) => role.id)) + 1
let nextOperationLogId = Math.max(...operationLogs.map((log) => log.id ?? 0)) + 1
const userPasswords = new Map<number, string>()

function requireUser(userId: number) {
  const user = users.find((item) => item.id === userId)
  if (!user) {
    throw new Error('用户不存在。')
  }
  return user
}

function requireRole(roleId: number) {
  const role = roles.find((item) => item.id === roleId)
  if (!role) {
    throw new Error('角色不存在。')
  }
  return role
}

function requireUniqueEmployeeNo(employeeNo: string, userId?: number) {
  if (!employeeNo.trim()) {
    throw new Error('工号不能为空。')
  }
  if (users.some((user) => user.id !== userId && user.employeeNo === employeeNo.trim())) {
    throw new Error('工号已存在。')
  }
}

function requireUniqueRoleName(name: string, roleId?: number) {
  if (!name.trim()) {
    throw new Error('角色名称不能为空。')
  }
  if (roles.some((role) => role.id !== roleId && role.name === name.trim())) {
    throw new Error('角色名称已存在。')
  }
}

function appendOperationLog(action: string, beforeData?: unknown, afterData?: unknown) {
  let serializedAfterData: string | undefined = undefined
  let serializedBeforeData: string | undefined = undefined
  if (afterData !== undefined) {
    serializedAfterData = JSON.stringify(afterData)
  }
  if (beforeData !== undefined) {
    serializedBeforeData = JSON.stringify(beforeData)
  }
  operationLogs.unshift({
    action,
    afterData: serializedAfterData,
    beforeData: serializedBeforeData,
    id: nextOperationLogId++,
    ipAddress: '127.0.0.1',
    module: '系统管理',
    operateTime: new Date().toISOString(),
    operatorId: 1002,
  })
}

function clone<TValue>(value: TValue): TValue {
  return structuredClone(value)
}

function paginate<TItem>(items: TItem[], page: number, pageSize: number): PageResult<TItem> {
  const safePage = Math.max(1, page)
  const safePageSize = Math.max(1, pageSize)
  const start = (safePage - 1) * safePageSize
  return {
    items: clone(items.slice(start, start + safePageSize)),
    page: safePage,
    pageSize: safePageSize,
    total: items.length,
  }
}

function matchesText(value: string | undefined, query: string | undefined) {
  return !query || value?.toLowerCase().includes(query.trim().toLowerCase())
}

function matchesTime(value: string | undefined, startTime?: string, endTime?: string) {
  return Boolean(value) && (!startTime || value! >= startTime) && (!endTime || value! <= endTime)
}

function withScenario<TValue>(scenario: MockScenario, factory: () => TValue): Promise<TValue> {
  return new Promise((resolve, reject) => {
    globalThis.setTimeout(() => {
      if (scenario === 'error') {
        reject(new Error('系统管理 Mock 数据加载失败，请稍后重试。'))
        return
      }
      resolve(factory())
    }, 80)
  })
}

function scenarioUsers(scenario: MockScenario) {
  if (scenario === 'empty') {
    return []
  }
  return users
}

function scenarioRoles(scenario: MockScenario) {
  if (scenario === 'empty') {
    return []
  }
  return roles
}

function scenarioPermissions(scenario: MockScenario) {
  if (scenario === 'empty') {
    return []
  }
  return permissions
}

function scenarioLoginLogs(scenario: MockScenario) {
  if (scenario === 'empty') {
    return []
  }
  return loginLogs
}

function scenarioOperationLogs(scenario: MockScenario) {
  if (scenario === 'empty') {
    return []
  }
  return operationLogs
}

function getOperatorName(operatorId: number, scenario: MockScenario) {
  return scenarioUsers(scenario).find((user) => user.id === operatorId)?.name
}

function getOptionalUserName(
  userId: number,
  includeUserDirectory: boolean,
  scenario: MockScenario,
) {
  if (!includeUserDirectory) {
    return undefined
  }
  return getOperatorName(userId, scenario)
}

function getOptionalEmployeeNo(userId: number, includeUserDirectory: boolean) {
  if (!includeUserDirectory) {
    return undefined
  }
  return users.find((user) => user.id === userId)?.employeeNo
}

function toRoleSummary(role: SystemRole): SystemRoleSummary {
  return {
    ...role,
    permissionCount: rolePermissions.filter((item) => item.roleId === role.id).length,
    userCount: userRoles.filter((item) => item.roleId === role.id).length,
  }
}

function buildPermissionTree(scenario: MockScenario): PermissionTreeNode[] {
  const groups = new Map<string, PermissionTreeNode>()
  for (const permission of scenarioPermissions(scenario)) {
    const group = groups.get(permission.resource) ?? {
      children: [],
      id: `resource:${permission.resource}`,
      label: permission.resource,
    }
    group.children.push({
      id: `permission:${permission.id}`,
      label: permission.action,
      permissionId: permission.id,
    })
    groups.set(permission.resource, group)
  }
  return [...groups.values()]
}

// The service methods are grouped by page capability rather than alphabetically.
// oxlint-disable-next-line sort-keys
export const systemMock = {
  assignRolePermissions(roleId: number, permissionIds: number[]) {
    const role = requireRole(roleId)
    const validPermissionIds = new Set(permissions.map((permission) => permission.id))
    if (!permissionIds.length || permissionIds.some((id) => !validPermissionIds.has(id))) {
      throw new Error('权限不存在或不能为空。')
    }
    const beforeData = rolePermissions.filter((item) => item.roleId === roleId)
    rolePermissions = [
      ...rolePermissions.filter((item) => item.roleId !== roleId),
      ...permissionIds.map((permissionId) => ({ permissionId, roleId })),
    ]
    appendOperationLog('分配角色权限', beforeData, { permissionIds, roleId: role.id })
  },

  assignUserRoles(userId: number, roleIds: number[]) {
    requireUser(userId)
    const validRoleIds = new Set(
      roles.filter((role) => role.status === 'valid').map((role) => role.id),
    )
    if (roleIds.some((roleId) => !validRoleIds.has(roleId))) {
      throw new Error('只能分配有效角色。')
    }
    const beforeData = userRoles.filter((item) => item.userId === userId)
    userRoles = [
      ...userRoles.filter((item) => item.userId !== userId),
      ...roleIds.map((roleId) => ({ roleId, userId })),
    ]
    appendOperationLog('分配用户角色', beforeData, { roleIds, userId })
  },

  createRole(form: RoleFormData) {
    requireUniqueRoleName(form.name)
    const role: SystemRole = {
      description: form.description?.trim() || undefined,
      id: nextRoleId++,
      name: form.name.trim(),
      status: form.status,
    }
    roles = [...roles, role]
    appendOperationLog('新增角色', undefined, role)
    return clone(role)
  },

  createUser(form: UserCreateFormData, passwordHash: string) {
    requireUniqueEmployeeNo(form.employeeNo)
    if (!form.name.trim() || !form.phone.trim() || !passwordHash) {
      throw new Error('用户信息不完整。')
    }
    const user: SystemUser = {
      createdTime: new Date().toISOString(),
      email: form.email?.trim() || undefined,
      employeeNo: form.employeeNo.trim(),
      id: nextUserId++,
      name: form.name.trim(),
      phone: form.phone.trim(),
      pwdUpdateTime: new Date().toISOString(),
      status: form.status,
    }
    users = [...users, user]
    userPasswords.set(user.id, passwordHash)
    appendOperationLog('新增用户', undefined, user)
    return clone(user)
  },

  listUsers(query: UserQuery, scenario: MockScenario) {
    return withScenario(scenario, () => {
      const filtered = scenarioUsers(scenario).filter(
        (user) =>
          matchesText(user.employeeNo, query.employeeNo) &&
          matchesText(user.name, query.userName) &&
          (!query.status || user.status === query.status),
      )
      return paginate(filtered, query.page, query.pageSize)
    })
  },

  listRoles(query: RoleQuery, scenario: MockScenario): Promise<PageResult<SystemRoleSummary>> {
    return withScenario(scenario, () => {
      const filtered = scenarioRoles(scenario).filter(
        (role) =>
          (!query.roleId || role.id === query.roleId) &&
          matchesText(role.name, query.roleName) &&
          (!query.status || role.status === query.status),
      )
      return paginate(filtered.map(toRoleSummary), query.page, query.pageSize)
    })
  },

  listRoleOptions(scenario: MockScenario): Promise<SystemRole[]> {
    return withScenario(scenario, () => clone(scenarioRoles(scenario)))
  },

  listUserRoleIds(userId: number, scenario: MockScenario): Promise<number[]> {
    return withScenario(scenario, () => {
      if (scenario === 'empty') {
        return []
      }
      return userRoles
        .filter((relation) => relation.userId === userId)
        .map((relation) => relation.roleId)
    })
  },

  resetUserPassword(userId: number, passwordHash: string) {
    const user = requireUser(userId)
    if (!passwordHash) {
      throw new Error('密码不能为空。')
    }
    userPasswords.set(userId, passwordHash)
    const beforeData = { pwdUpdateTime: user.pwdUpdateTime }
    user.pwdUpdateTime = new Date().toISOString()
    appendOperationLog('重置用户密码', beforeData, { pwdUpdateTime: user.pwdUpdateTime, userId })
  },

  updateRole(roleId: number, form: RoleFormData) {
    const role = requireRole(roleId)
    requireUniqueRoleName(form.name, roleId)
    const beforeData = clone(role)
    role.description = form.description?.trim() || undefined
    role.name = form.name.trim()
    role.status = form.status
    appendOperationLog('编辑角色', beforeData, role)
    return clone(role)
  },

  updateRoleStatus(roleId: number, status: AccountStatus) {
    const role = requireRole(roleId)
    const beforeData = clone(role)
    role.status = status
    appendOperationLog('更新角色状态', beforeData, role)
  },

  updateUser(userId: number, form: UserFormData) {
    const user = requireUser(userId)
    requireUniqueEmployeeNo(form.employeeNo, userId)
    if (!form.name.trim() || !form.phone.trim()) {
      throw new Error('用户信息不完整。')
    }
    const beforeData = clone(user)
    user.email = form.email?.trim() || undefined
    user.employeeNo = form.employeeNo.trim()
    user.name = form.name.trim()
    user.phone = form.phone.trim()
    user.status = form.status
    appendOperationLog('编辑用户', beforeData, user)
    return clone(user)
  },

  updateUserStatus(userId: number, status: AccountStatus) {
    const user = requireUser(userId)
    const beforeData = clone(user)
    user.status = status
    appendOperationLog('更新用户状态', beforeData, user)
  },

  loadRolePermissionAssignment(
    roleId: number,
    scenario: MockScenario,
  ): Promise<RolePermissionAssignment> {
    return withScenario(scenario, () => {
      const tree = buildPermissionTree(scenario)
      const allPermissionNodeKeys = tree.flatMap((group) => group.children.map((item) => item.id))
      const checkedPermissionNodeKeys = rolePermissions
        .filter((item) => item.roleId === roleId)
        .map((item) => `permission:${item.permissionId}`)
      return { allPermissionNodeKeys, checkedPermissionNodeKeys, tree }
    })
  },

  listLoginLogs(
    query: LoginLogQuery,
    includeUserDirectory: boolean,
    scenario: MockScenario,
  ): Promise<PageResult<SystemLoginLog>> {
    return withScenario(scenario, () => {
      const filtered = scenarioLoginLogs(scenario).filter(
        (log) =>
          (!query.userId || log.userId === query.userId) &&
          (!query.result || log.result === query.result) &&
          matchesTime(log.loginTime, query.startTime, query.endTime),
      )
      const items = filtered.map((log) => {
        const item = structuredClone(log)
        item.employeeNo = getOptionalEmployeeNo(log.userId, includeUserDirectory)
        item.userName = getOptionalUserName(log.userId, includeUserDirectory, scenario)
        return item
      })
      return paginate(items, query.page, query.pageSize)
    })
  },

  listOperationLogs(
    query: OperationLogQuery,
    includeUserDirectory: boolean,
    scenario: MockScenario,
  ): Promise<PageResult<SystemOperationLog>> {
    return withScenario(scenario, () => {
      const filtered = scenarioOperationLogs(scenario).filter(
        (log) =>
          (!query.operatorId || log.operatorId === query.operatorId) &&
          matchesText(log.module, query.module) &&
          matchesText(log.action, query.action) &&
          matchesTime(log.operateTime, query.startTime, query.endTime),
      )
      const items = filtered.map((log) => {
        const item = structuredClone(log)
        item.operatorName = getOptionalUserName(log.operatorId, includeUserDirectory, scenario)
        return item
      })
      return paginate(items, query.page, query.pageSize)
    })
  },
}
