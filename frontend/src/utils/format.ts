import { BUSINESS_TIME_ZONE, parseApiTime } from './time'

export function formatDateTime(value?: string | null) {
  if (!value) {
    return '-'
  }

  // 纯业务日期没有时区，也不应显示虚构的午夜时间。
  if (/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return value
  }
  const date = parseApiTime(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }
  return date.toLocaleString('zh-CN', { hour12: false, timeZone: BUSINESS_TIME_ZONE })
}

export function formatNumber(value?: number | null, maximumFractionDigits = 2) {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '-'
  }
  return new Intl.NumberFormat('zh-CN', { maximumFractionDigits }).format(value)
}

export function formatAmount(value?: number | null, currency = 'CNY') {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '-'
  }
  return new Intl.NumberFormat('zh-CN', { currency, style: 'currency' }).format(value)
}
