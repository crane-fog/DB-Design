/** 统一分页结果结构，供各业务 Service 复用。 */
export interface PageRequest {
  page: number
  pageSize: number
}

export interface PageResult<TEntity> {
  items: TEntity[]
  page: number
  pageSize: number
  total: number
}

/** 后端返回的通用响应信封。 */
export interface ApiEnvelope<TPayload> {
  code?: number
  data?: TPayload
  message?: string
}

/** 后端分页负载的原始形态，兼容多种字段命名。 */
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

/** 接口业务错误，携带后端返回的状态码。 */
export class ApiRequestError extends Error {
  readonly status: number | undefined
  readonly responseData: unknown

  constructor(message: string, status?: number, responseData?: unknown) {
    super(message)
    this.name = 'ApiRequestError'
    this.status = status
    this.responseData = responseData
  }
}

/** 校验响应信封并返回业务数据，非成功码抛出 ApiRequestError。 */
export function unwrap<TPayload>(payload: ApiEnvelope<TPayload>): TPayload | undefined {
  if (payload.code !== undefined && payload.code !== 200) {
    throw new ApiRequestError(payload.message || '接口请求失败', payload.code)
  }
  return payload.data
}

/** 将接口分页 DTO 转为页面唯一使用的分页结构。 */
export function mapPageResult<TSource, TResult>(
  payload: unknown,
  fallback: PageRequest,
  mapper: (item: TSource) => TResult,
): PageResult<TResult> {
  const items = getPageItems<TSource>(payload).map(mapper)
  const metadata = getPageMetadata(payload, { ...fallback, total: items.length })
  return { items, ...metadata }
}

/** 从多种分页负载形态中提取条目数组。 */
export function getPageItems<TItem>(value: unknown): TItem[] {
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

/** 从分页负载中提取分页元数据，缺失时回退到给定默认值。 */
export function getPageMetadata(
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

/** 非空字符串返回原值，否则返回 undefined。 */
export function optionalText(value: unknown) {
  if (typeof value === 'string' && value.trim()) {
    return value.trim()
  }
  return undefined
}

/** 有限数字返回原值，否则返回 undefined。 */
export function optionalNumber(value: unknown) {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value
  }
  return undefined
}

/** 去除首尾空白后返回非空字符串，否则返回 undefined。 */
export function nullableText(value: string | undefined) {
  return value?.trim() || undefined
}
