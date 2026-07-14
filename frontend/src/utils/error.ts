import axios from 'axios'

export function getErrorMessage(error: unknown, fallback = '请求失败，请稍后重试') {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data
    if (typeof data === 'object' && data !== null) {
      const message =
        (data as { message?: unknown; msg?: unknown }).message ?? (data as { msg?: unknown }).msg
      if (typeof message === 'string' && message.trim()) {
        return message
      }
    }
  }

  if (error instanceof Error && error.message) {
    return error.message
  }
  return fallback
}
