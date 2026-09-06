import { Api, systemApi } from '@/api/client'
import {
  type ApiEnvelope,
  type PageRequest,
  type PageResult,
  getPageItems,
  getPageMetadata,
  optionalText,
  unwrap,
} from '@/services/pagination'
import type {
  CurrentAccessData,
  LoginData,
  LoginLog,
  LoginRequest,
  OperationLog,
  Permission,
  RegisterRequest,
  Role,
  RolePermission,
  User,
  UserRole,
} from '@/api'
import { PERMISSIONS } from '@/constants/permissions'
import { getRequestStatus } from '@/services/request'
import { toUtcDateTime } from '@/utils/time'

export { getRequestStatus }
export type { PageResult }

export type AccountStatus = 'disabled' | 'valid'

export interface UserQuery extends PageRequest {
  employeeNo?: string
  status?: AccountStatus
  userName?: string
}

export interface RoleQuery extends PageRequest {
  roleId?: number
  roleName?: string
  status?: AccountStatus
}

export type LoginResult = 'failure' | 'success'

export interface LoginLogQuery extends PageRequest {
  endTime?: string
  result?: LoginResult
  startTime?: string
  userId?: number
}

export interface OperationLogQuery extends PageRequest {
  action?: string
  endTime?: string
  module?: string
  operatorId?: number
  startTime?: string
}

export interface SystemUser {
  createdTime?: string
  email?: string
  employeeNo: string
  id: number
  lastLoginTime?: string
  name: string
  phone: string
  pwdUpdateTime?: string
  status: AccountStatus
}

export interface UserFormData {
  email?: string
  employeeNo: string
  name: string
  phone: string
  status: AccountStatus
}

export interface UserCreateFormData extends UserFormData {
  password: string
}

export interface RegisterFormData {
  email?: string
  employeeNo: string
  password: string
  phone: string
  userName: string
}

export interface SystemRole {
  description?: string
  id: number
  name: string
  status: AccountStatus
}

export interface SystemRoleSummary extends SystemRole {
  permissionCount: number
  userCount: number
}

export interface RoleFormData {
  description?: string
  name: string
  status: AccountStatus
}

export interface PermissionTreeLeaf {
  id: string
  label: string
  permissionId: number
}

export interface PermissionTreeNode {
  children: PermissionTreeLeaf[]
  id: string
  label: string
}

export interface RolePermissionAssignment {
  allPermissionNodeKeys: string[]
  checkedPermissionNodeKeys: string[]
  tree: PermissionTreeNode[]
}

export interface SystemLoginLog {
  employeeNo?: string
  failReason?: string
  id?: number
  ipAddress?: string
  loginTime?: string
  result?: LoginResult
  userId?: number
  userName?: string
}

export interface SystemOperationLog {
  action?: string
  afterData?: string
  beforeData?: string
  id?: number
  ipAddress?: string
  module?: string
  operateTime?: string
  operatorId?: number
  operatorName?: string
}

export interface SystemAccessContext {
  currentUser: Pick<SystemUser, 'employeeNo' | 'name'> & { id?: number }
  permissions: string[]
  roles: string[]
}

function asAccountStatus(value: unknown): AccountStatus {
  if (value === 'disabled') {
    return 'disabled'
  }
  return 'valid'
}

function toSystemUser(user: User): SystemUser | undefined {
  if (typeof user.user_id !== 'number') {
    return undefined
  }

  return {
    createdTime: optionalText(user.created_time),
    email: optionalText(user.email),
    employeeNo: user.employee_no ?? '',
    id: user.user_id,
    lastLoginTime: optionalText(user.last_login_time),
    name: user.user_name ?? '',
    phone: user.phone ?? '',
    pwdUpdateTime: optionalText(user.pwd_update_time),
    status: asAccountStatus(user.status),
  }
}

function toSystemRole(role: Role): SystemRole | undefined {
  if (typeof role.role_id !== 'number') {
    return undefined
  }

  return {
    description: optionalText(role.description),
    id: role.role_id,
    name: role.role_name ?? '',
    status: asAccountStatus(role.status),
  }
}

const permissionResourceCodes: Record<string, string> = {
  BOM: 'material',
  外部订单: 'external-order',
  库存: 'inventory',
  物料: 'material',
  生产线: 'production',
  生产订单: 'production',
  用户管理: 'system:user',
  质量追溯: 'trace',
  采购订单: 'purchase',
}

const permissionActionCodes: Record<string, string> = {
  修改: 'update',
  创建: 'create',
  审核: 'approve',
  查看: 'view',
}

function toPermissionCode(permission: Permission) {
  const resource = permission.resource && permissionResourceCodes[permission.resource]
  const action = permission.action && permissionActionCodes[permission.action]
  if (!resource || !action) {
    return undefined
  }
  return `${resource}:${action}`
}

function toLoginResult(value: unknown): LoginResult | undefined {
  if (value === 'failure' || value === 'success') {
    return value
  }
  return undefined
}

function formatJsonSnapshot(value: unknown) {
  if (value === undefined || value === null) {
    return undefined
  }
  if (typeof value === 'string') {
    try {
      const parsed = JSON.parse(value)
      const formatted = JSON.stringify(parsed, undefined, 2)
      if (formatted) {
        return formatted
      }
    } catch {
      return value
    }
    return value
  }

  const formatted = JSON.stringify(value, undefined, 2)
  if (formatted) {
    return formatted
  }
  return String(value)
}

function toSystemLoginLog(log: LoginLog, users: Map<number, SystemUser>): SystemLoginLog {
  let user: SystemUser | undefined = undefined
  if (typeof log.user_id === 'number') {
    user = users.get(log.user_id)
  }
  return {
    employeeNo: user?.employeeNo,
    failReason: optionalText(log.fail_reason),
    id: log.log_id,
    ipAddress: optionalText(log.ip_address),
    loginTime: optionalText(log.login_time),
    result: toLoginResult(log.result),
    userId: log.user_id ?? undefined,
    userName: user?.name,
  }
}

function toSystemOperationLog(
  log: OperationLog,
  users: Map<number, SystemUser>,
): SystemOperationLog {
  let user: SystemUser | undefined = undefined
  if (typeof log.operator_id === 'number') {
    user = users.get(log.operator_id)
  }
  return {
    action: optionalText(log.action),
    afterData: formatJsonSnapshot(log.after_data),
    beforeData: formatJsonSnapshot(log.before_data),
    id: log.log_id,
    ipAddress: optionalText(log.ip_address),
    module: optionalText(log.module),
    operateTime: optionalText(log.operate_time),
    operatorId: log.operator_id,
    operatorName: user?.name,
  }
}

function toPermissionTreeLeaf(permission: Permission): PermissionTreeLeaf | undefined {
  if (typeof permission.permission_id !== 'number') {
    return undefined
  }

  const action = optionalText(permission.action) ?? '未命名操作'
  return {
    id: `permission:${permission.permission_id}`,
    label: action,
    permissionId: permission.permission_id,
  }
}

function buildPermissionTree(permissions: Permission[]) {
  const groups = new Map<string, PermissionTreeNode>()
  for (const permission of permissions) {
    const leaf = toPermissionTreeLeaf(permission)
    if (leaf) {
      const resource = optionalText(permission.resource) ?? '未分类资源'
      const group = groups.get(resource) ?? {
        children: [],
        id: `resource:${resource}`,
        label: resource,
      }
      group.children.push(leaf)
      groups.set(resource, group)
    }
  }

  const tree: PermissionTreeNode[] = []
  for (const group of groups.values()) {
    tree.push({ children: group.children, id: group.id, label: group.label })
  }
  return tree
}

function toPermissionId(nodeKey: unknown) {
  if (typeof nodeKey !== 'string' || !nodeKey.startsWith('permission:')) {
    return undefined
  }
  const permissionId = Number(nodeKey.slice('permission:'.length))
  if (Number.isInteger(permissionId) && permissionId > 0) {
    return permissionId
  }
  return undefined
}

async function hashPassword(value: string) {
  const passwordBytes = new TextEncoder().encode(value)
  const hashBuffer = await crypto.subtle.digest('SHA-256', passwordBytes)

  return [...new Uint8Array(hashBuffer)].map((byte) => byte.toString(16).padStart(2, '0')).join('')
}

function toNullableText(value: string | undefined) {
  return value?.trim() || undefined
}

function countRoleRelations<TItem extends { role_id?: number }>(relations: TItem[]) {
  const counts = new Map<number, number>()
  for (const relation of relations) {
    if (typeof relation.role_id === 'number') {
      counts.set(relation.role_id, (counts.get(relation.role_id) ?? 0) + 1)
    }
  }
  return counts
}

async function getAllPageItems<TItem>(
  request: (page: number, pageSize: number) => Promise<{ data: unknown }>,
) {
  const pageSize = 100
  async function loadPage(page: number, allItems: TItem[]): Promise<TItem[]> {
    const response = await request(page, pageSize)
    const data = unwrap(response.data as ApiEnvelope<unknown>)
    const items = getPageItems<TItem>(data)
    allItems.push(...items)
    const metadata = getPageMetadata(data, { page, pageSize, total: allItems.length })
    if (!items.length || allItems.length >= metadata.total) {
      return allItems
    }
    return loadPage(page + 1, allItems)
  }

  return loadPage(1, [])
}

async function getAuditUsers() {
  const users = await getAllPageItems<User>((page, pageSize) =>
    systemApi.listUserData({ page, pageSize }),
  )
  const userMap = new Map<number, SystemUser>()
  for (const user of users) {
    const systemUser = toSystemUser(user)
    if (systemUser) {
      userMap.set(systemUser.id, systemUser)
    }
  }
  return userMap
}

async function loadCurrentAccess(): Promise<SystemAccessContext> {
  const response = await systemApi.getCurrentAccess()
  const access = unwrap(response.data as ApiEnvelope<CurrentAccessData>)
  const currentUser = access?.current_user

  if (!access || !currentUser || !access.roles || !access.permissions) {
    throw new Error('当前登录用户的权限数据不完整')
  }

  // uniqueItems 被生成为 Set，实际 JSON 为数组；按可迭代集合读取以兼容两者。
  const roles = [...new Set(access.roles)]
  const permissionCodes = new Set(
    [...access.permissions]
      .map(toPermissionCode)
      .filter((permission): permission is string => Boolean(permission)),
  )

  if (roles.includes('系统管理员')) {
    Object.values(PERMISSIONS)
      .flatMap((modulePermissions) => Object.values(modulePermissions))
      .forEach((permission) => permissionCodes.add(permission))
  }

  // 将后端返回的业务角色映射到页面入口；与 UserContextService 的角色检查保持一致。
  if (roles.includes('生产管理员')) {
    Object.values(PERMISSIONS.production).forEach((permission) => permissionCodes.add(permission))
    Object.values(PERMISSIONS.material).forEach((permission) => permissionCodes.add(permission))
  }
  if (roles.some((role) => role === '生产管理员' || role === '库存管理员')) {
    Object.values(PERMISSIONS.inventory).forEach((permission) => permissionCodes.add(permission))
  }
  if (roles.some((role) => role === '采购员' || role === '采购主管')) {
    Object.values(PERMISSIONS.purchase).forEach((permission) => permissionCodes.add(permission))
  }
  if (roles.includes('采购员')) {
    permissionCodes.add(PERMISSIONS.material.view)
  }
  if (roles.includes('外部客户')) {
    permissionCodes.add(PERMISSIONS.production.view)
  }
  if (roles.includes('质量管理员')) {
    Object.values(PERMISSIONS.trace).forEach((permission) => permissionCodes.add(permission))
  }

  return {
    currentUser: {
      employeeNo: currentUser.employee_no,
      id: currentUser.user_id,
      name: currentUser.user_name,
    },
    permissions: [...permissionCodes],
    roles,
  }
}

export const systemService = {
  async assignRolePermissions(roleId: number, permissionIds: number[]) {
    if (!permissionIds.length) {
      throw new Error('请至少选择一个权限')
    }
    const response = await systemApi.addRolePermission({
      rolePermissionAssignRequest: { permission_ids: permissionIds, role_id: roleId },
    })
    return unwrap(response.data as ApiEnvelope<unknown>)
  },

  async assignUserRoles(userId: number, roleIds: number[]) {
    const response = await systemApi.addUserRole({
      userRoleAssignRequest: { role_ids: roleIds, user_id: userId },
    })
    return unwrap(response.data as ApiEnvelope<unknown>)
  },

  async createRole(form: RoleFormData) {
    const response = await systemApi.addRoleData({
      roleCreateRequest: {
        description: toNullableText(form.description),
        role_name: form.name.trim(),
        status: form.status,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<Role | undefined>)
    if (data) {
      return toSystemRole(data)
    }
    return undefined
  },

  async createUser(form: UserCreateFormData) {
    const response = await systemApi.addUserData({
      userCreateRequest: {
        email: toNullableText(form.email),
        employee_no: form.employeeNo.trim(),
        password: await hashPassword(form.password),
        phone: form.phone.trim(),
        status: form.status,
        user_name: form.name.trim(),
      },
    })
    const data = unwrap(response.data as ApiEnvelope<User | undefined>)
    if (data) {
      return toSystemUser(data)
    }
    return undefined
  },

  getPermissionIdsFromNodeKeys(nodeKeys: unknown[]) {
    return nodeKeys
      .map(toPermissionId)
      .filter((permissionId): permissionId is number => permissionId !== undefined)
  },

  getUserTest: () => Api.getUserTest(),

  async listLoginLogs(
    query: LoginLogQuery,
    includeUserDirectory = false,
  ): Promise<PageResult<SystemLoginLog>> {
    const response = await systemApi.listLoginRecordData({
      endTime: toUtcDateTime(query.endTime || undefined),
      page: query.page,
      pageSize: query.pageSize,
      result: query.result,
      startTime: toUtcDateTime(query.startTime || undefined),
      userId: query.userId,
    })
    let users = new Map<number, SystemUser>()
    if (includeUserDirectory) {
      users = await getAuditUsers()
    }
    const data = unwrap(response.data as ApiEnvelope<unknown>)
    const items = getPageItems<LoginLog>(data).map((log) => toSystemLoginLog(log, users))
    const metadata = getPageMetadata(data, {
      page: query.page,
      pageSize: query.pageSize,
      total: items.length,
    })

    return { items, ...metadata }
  },

  async listOperationLogs(
    query: OperationLogQuery,
    includeUserDirectory = false,
  ): Promise<PageResult<SystemOperationLog>> {
    const response = await systemApi.listOperationLogData({
      action: query.action || undefined,
      endTime: toUtcDateTime(query.endTime || undefined),
      module: query.module || undefined,
      operatorId: query.operatorId,
      page: query.page,
      pageSize: query.pageSize,
      startTime: toUtcDateTime(query.startTime || undefined),
    })
    let users = new Map<number, SystemUser>()
    if (includeUserDirectory) {
      users = await getAuditUsers()
    }
    const data = unwrap(response.data as ApiEnvelope<unknown>)
    const items = getPageItems<OperationLog>(data).map((log) => toSystemOperationLog(log, users))
    const metadata = getPageMetadata(data, {
      page: query.page,
      pageSize: query.pageSize,
      total: items.length,
    })

    return { items, ...metadata }
  },

  async listRolePage(query: RoleQuery): Promise<PageResult<SystemRoleSummary>> {
    const response = await systemApi.listRoleData({
      page: query.page,
      pageSize: query.pageSize,
      roleId: query.roleId,
      roleName: query.roleName || undefined,
      status: query.status,
    })
    const data = unwrap(response.data as ApiEnvelope<unknown>)
    const [userRelations, permissionRelations] = await Promise.all([
      getAllPageItems<UserRole>((page, pageSize) => systemApi.listUserRoleData({ page, pageSize })),
      getAllPageItems<RolePermission>((page, pageSize) =>
        systemApi.listRolePermissionData({ page, pageSize }),
      ),
    ])
    const userCounts = countRoleRelations(userRelations)
    const permissionCounts = countRoleRelations(permissionRelations)
    const items = getPageItems<Role>(data)
      .map(toSystemRole)
      .filter((role): role is SystemRole => Boolean(role))
      .map((role) => ({
        description: role.description,
        id: role.id,
        name: role.name,
        permissionCount: permissionCounts.get(role.id) ?? 0,
        status: role.status,
        userCount: userCounts.get(role.id) ?? 0,
      }))
    const metadata = getPageMetadata(data, {
      page: query.page,
      pageSize: query.pageSize,
      total: items.length,
    })

    return { items, ...metadata }
  },

  async listRoles(): Promise<SystemRole[]> {
    const response = await systemApi.listRoleData({ page: 1, pageSize: 100 })
    const data = unwrap(response.data as ApiEnvelope<unknown>)

    return getPageItems<Role>(data)
      .map(toSystemRole)
      .filter((role): role is SystemRole => Boolean(role))
  },

  async listUserRoleIds(userId: number) {
    const response = await systemApi.listUserRoleData({ page: 1, pageSize: 100, userId })
    const data = unwrap(response.data as ApiEnvelope<unknown>)

    return getPageItems<UserRole>(data)
      .filter((relation) => relation.user_id === userId && typeof relation.role_id === 'number')
      .map((relation) => relation.role_id as number)
  },

  async listUsers(query: UserQuery): Promise<PageResult<SystemUser>> {
    const response = await systemApi.listUserData({
      employeeNo: query.employeeNo || undefined,
      page: query.page,
      pageSize: query.pageSize,
      status: query.status,
      userName: query.userName || undefined,
    })
    const data = unwrap(response.data as ApiEnvelope<unknown>)
    const items = getPageItems<User>(data)
      .map(toSystemUser)
      .filter((user): user is SystemUser => Boolean(user))
    const metadata = getPageMetadata(data, {
      page: query.page,
      pageSize: query.pageSize,
      total: items.length,
    })

    return { items, ...metadata }
  },

  loadCurrentAccess,

  async loadRolePermissionAssignment(roleId: number): Promise<RolePermissionAssignment> {
    const [permissions, rolePermissions] = await Promise.all([
      getAllPageItems<Permission>((page, pageSize) =>
        systemApi.listPermissionData({ page, pageSize }),
      ),
      getAllPageItems<RolePermission>((page, pageSize) =>
        systemApi.listRolePermissionData({ page, pageSize, roleId }),
      ),
    ])
    const tree = buildPermissionTree(permissions)
    const allPermissionNodeKeys = tree.flatMap((group) =>
      group.children.map((permission) => permission.id),
    )
    const checkedPermissionNodeKeys = rolePermissions
      .filter(
        (relation) => relation.role_id === roleId && typeof relation.permission_id === 'number',
      )
      .map((relation) => `permission:${relation.permission_id as number}`)

    return { allPermissionNodeKeys, checkedPermissionNodeKeys, tree }
  },

  async login(request: LoginRequest) {
    const response = await systemApi.login({ loginRequest: request })
    return unwrap(response.data as ApiEnvelope<LoginData>)
  },

  async register(form: RegisterFormData) {
    const request: RegisterRequest = {
      email: toNullableText(form.email),
      employee_no: form.employeeNo.trim(),
      password: await hashPassword(form.password),
      phone: form.phone.trim(),
      user_name: form.userName.trim(),
    }
    const response = await systemApi.register({ registerRequest: request })
    return unwrap(response.data as ApiEnvelope<unknown>)
  },

  async resetUserPassword(userId: number, password: string) {
    const response = await systemApi.updateUserData({
      userUpdateRequest: { password: await hashPassword(password), user_id: userId },
    })
    return unwrap(response.data as ApiEnvelope<unknown>)
  },

  async updateRole(roleId: number, form: RoleFormData) {
    const response = await systemApi.updateRoleData({
      roleUpdateRequest: {
        description: toNullableText(form.description),
        role_id: roleId,
        role_name: form.name.trim(),
        status: form.status,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<Role | undefined>)
    if (data) {
      return toSystemRole(data)
    }
    return undefined
  },

  async updateRoleStatus(role: SystemRole, status: AccountStatus) {
    const response = await systemApi.updateRoleData({
      roleUpdateRequest: {
        description: toNullableText(role.description),
        role_id: role.id,
        role_name: role.name,
        status,
      },
    })
    return unwrap(response.data as ApiEnvelope<unknown>)
  },

  async updateUser(userId: number, form: UserFormData) {
    const response = await systemApi.updateUserData({
      userUpdateRequest: {
        email: toNullableText(form.email),
        employee_no: form.employeeNo.trim(),
        phone: form.phone.trim(),
        status: form.status,
        user_id: userId,
        user_name: form.name.trim(),
      },
    })
    const data = unwrap(response.data as ApiEnvelope<User | undefined>)
    if (data) {
      return toSystemUser(data)
    }
    return undefined
  },

  async updateUserStatus(userId: number, status: AccountStatus) {
    const response = await systemApi.updateUserData({
      userUpdateRequest: { status, user_id: userId },
    })
    return unwrap(response.data as ApiEnvelope<unknown>)
  },
}
