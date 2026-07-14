import { Api, systemApi } from '@/api/client'
import type { LoginData, LoginRequest, RegisterRequest, User } from '@/api'

export interface UserQuery {
  employeeNo?: string
  page: number
  pageSize: number
  status?: 'disabled' | 'valid'
  userName?: string
}

export interface PageResult<TEntity> {
  items: TEntity[]
  page: number
  pageSize: number
  total: number
}

interface ApiEnvelope<TPayload> {
  code?: number
  data?: TPayload
  message?: string
}

function unwrap<TPayload>(payload: ApiEnvelope<TPayload>) {
  if (payload.code !== undefined && payload.code !== 200) {
    throw new Error(payload.message || '接口请求失败')
  }

  return payload.data
}

function getUserArray(value: unknown): User[] {
  if (Array.isArray(value)) {
    return value as User[]
  }
  if (!value || typeof value !== 'object') {
    return []
  }

  const data = value as { items?: unknown; list?: unknown; records?: unknown; rows?: unknown }
  const items = data.items ?? data.list ?? data.records ?? data.rows
  if (Array.isArray(items)) {
    return items as User[]
  }
  return []
}

function getTotal(value: unknown, fallback: number) {
  if (!value || typeof value !== 'object') {
    return fallback
  }
  const { total } = value as { total?: unknown }
  if (typeof total === 'number' && Number.isFinite(total)) {
    return total
  }
  return fallback
}

export const systemService = {
  getUserTest: () => Api.getUserTest(),

  async listUsers(query: UserQuery): Promise<PageResult<User>> {
    const response = await systemApi.listUserData({
      employeeNo: query.employeeNo || undefined,
      page: query.page,
      pageSize: query.pageSize,
      status: query.status,
      userName: query.userName || undefined,
    })
    const data = unwrap(response.data as ApiEnvelope<unknown>)
    const items = getUserArray(data)

    return {
      items,
      page: query.page,
      pageSize: query.pageSize,
      total: getTotal(data, items.length),
    }
  },

  async login(request: LoginRequest) {
    const response = await systemApi.login({ loginRequest: request })
    return unwrap(response.data as ApiEnvelope<LoginData>)
  },

  async register(request: RegisterRequest) {
    const response = await systemApi.register({ registerRequest: request })
    return unwrap(response.data as ApiEnvelope<unknown>)
  },
}
