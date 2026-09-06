export const BUSINESS_TIME_ZONE = 'Asia/Shanghai'

const dateFormatter = new Intl.DateTimeFormat('en-CA', {
  day: '2-digit',
  month: '2-digit',
  timeZone: BUSINESS_TIME_ZONE,
  year: 'numeric',
})

const dateTimeFormatter = new Intl.DateTimeFormat('en-CA', {
  day: '2-digit',
  hour: '2-digit',
  hourCycle: 'h23',
  minute: '2-digit',
  month: '2-digit',
  second: '2-digit',
  timeZone: BUSINESS_TIME_ZONE,
  year: 'numeric',
})

function parts(formatter: Intl.DateTimeFormat, date: Date) {
  return Object.fromEntries(formatter.formatToParts(date).map((part) => [part.type, part.value]))
}

// API 时间点应带 Z/偏移量；兼容旧接口中省略 Z 的 UTC 时间点。
export function parseApiTime(value: string): Date {
  let normalized = value.trim()
  if (!/(?:Z|[+-]\d{2}:?\d{2})$/i.test(normalized)) {
    normalized += 'Z'
  }
  return new Date(normalized)
}

export function businessDate(date = new Date()): string {
  const { year, month, day } = parts(dateFormatter, date)
  return `${year}-${month}-${day}`
}

// 时间选择器使用不含时区的北京时间字符串，避免组件跟随电脑时区转换。
export function toBusinessDateTimeInput(value?: string | null): string {
  if (!value) {
    return ''
  }
  const date = parseApiTime(value)
  if (Number.isNaN(date.getTime())) {
    return ''
  }
  const { year, month, day, hour, minute, second } = parts(dateTimeFormatter, date)
  return `${year}-${month}-${day}T${hour}:${minute}:${second}`
}

// 表单中的无时区时间按北京时间解释，已带时区的值保留原时间点。
export function toUtcDateTime(value: string): string
export function toUtcDateTime(value: string | null | undefined): string | undefined
export function toUtcDateTime(value: string | null | undefined): string | undefined {
  if (value === null || value === undefined) {
    return undefined
  }
  if (!value.trim()) {
    throw new Error('日期时间不能为空')
  }
  let normalized = value.trim()
  if (!/(?:Z|[+-]\d{2}:?\d{2})$/i.test(normalized)) {
    normalized += '+08:00'
  }
  const date = new Date(normalized)
  if (Number.isNaN(date.getTime())) {
    throw new Error('日期时间格式无效')
  }
  return date.toISOString()
}

export function toUtcDayBoundary(value: string | undefined, endOfDay: boolean): string | undefined {
  if (!value) {
    return undefined
  }
  if (value.length > 10) {
    return toUtcDateTime(value)
  }
  let time = '00:00:00.000'
  if (endOfDay) {
    time = '23:59:59.999'
  }
  return toUtcDateTime(`${value}T${time}`)
}
