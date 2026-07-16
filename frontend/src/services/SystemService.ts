import { Api, systemApi } from '@/api/client'
import type {
  LoginData,
  LoginRequest,
  RegisterRequest,
  Role,
  RolePermission,
  User,
  UserRole,
} from '@/api'

export type AccountStatus = 'disabled' | 'valid'

export interface UserQuery {
  employeeNo?: string
  page: number
  pageSize: number
  status?: AccountStatus
  userName?: string
}

export interface RoleQuery {
  page: number
  pageSize: number
  roleId?: number
  roleName?: string
  status?: AccountStatus
}

export interface PageResult<TEntity> {
  items: TEntity[]
  page: number
  pageSize: number
  total: number
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

interface ApiEnvelope<TPayload> {
  code?: number
  data?: TPayload
  message?: string
}

interface RawPageResult {
  items?: unknown
  list?: unknown
  page?: unknown
  page_size?: unknown
  pageSize?: unknown
  records?: unknown
  rows?: unknown
  total?: unknown
}

function unwrap<TPayload>(payload: ApiEnvelope<TPayload>) {
  if (payload.code !== undefined && payload.code !== 200) {
    throw new Error(payload.message || '接口请求失败')
  }

  return payload.data
}

function getPageItems<TItem>(value: unknown): TItem[] {
  if (Array.isArray(value)) {
    return value as TItem[]
  }
  if (!value || typeof value !== 'object') {
    return []
  }

  const data = value as RawPageResult
  const items = data.records ?? data.items ?? data.list ?? data.rows
  if (Array.isArray(items)) {
    return items as TItem[]
  }
  return []
}

function getPageMetadata(
  value: unknown,
  fallback: Pick<PageResult<unknown>, 'page' | 'pageSize' | 'total'>,
) {
  if (!value || typeof value !== 'object') {
    return fallback
  }

  const data = value as RawPageResult
  let { page } = fallback
  let { pageSize } = fallback
  let { total } = fallback
  if (typeof data.page === 'number') {
    ;({ page } = data)
  }
  if (typeof data.page_size === 'number') {
    pageSize = data.page_size
  } else if (typeof data.pageSize === 'number') {
    ;({ pageSize } = data)
  }
  if (typeof data.total === 'number') {
    ;({ total } = data)
  }

  return { page, pageSize, total }
}

function asAccountStatus(value: unknown): AccountStatus {
  if (value === 'disabled') {
    return 'disabled'
  }
  return 'valid'
}

function optionalText(value: unknown) {
  if (typeof value === 'string' && value.trim()) {
    return value
  }
  return undefined
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

export const systemService = {
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

  getUserTest: () => Api.getUserTest(),

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

  async login(request: LoginRequest) {
    const response = await systemApi.login({ loginRequest: request })
    return unwrap(response.data as ApiEnvelope<LoginData>)
  },

  async register(request: RegisterRequest) {
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
