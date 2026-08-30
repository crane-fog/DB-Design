import { getRequestErrorMessage } from '@/services/request'

export function getErrorMessage(error: unknown, fallback = '请求失败，请稍后重试') {
  const message = getRequestErrorMessage(error)
  if (message === '请求失败') {
    return fallback
  }
  return message
}
