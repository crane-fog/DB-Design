import { ApiRequestError } from '@/services/pagination'
import axios from 'axios'

interface ErrorPayload {
  message?: unknown
}

/** Extracts a meaningful API error without presenting UI from the service layer. */
export function getRequestErrorMessage(error: unknown) {
  if (error instanceof ApiRequestError) {
    return error.message
  }
  if (axios.isAxiosError(error)) {
    const message = (error.response?.data as ErrorPayload | undefined)?.message
    if (typeof message === 'string' && message.trim()) {
      return message.trim()
    }
    return error.message || '请求失败'
  }
  if (error instanceof Error) {
    return error.message
  }
  return '请求失败'
}

export function getRequestStatus(error: unknown) {
  if (error instanceof ApiRequestError) {
    return error.status
  }
  if (axios.isAxiosError(error)) {
    return error.response?.status
  }
  return undefined
}

/** Removes empty optional query values and trims text at the Service boundary. */
export function cleanQuery<TQuery extends object>(query: TQuery): TQuery {
  return Object.fromEntries(
    Object.entries(query).flatMap(([key, value]) => {
      if (typeof value === 'string') {
        const text = value.trim()
        if (text) {
          return [[key, text]]
        }
        return []
      }
      if (value === undefined) {
        return []
      }
      return [[key, value]]
    }),
  ) as TQuery
}
