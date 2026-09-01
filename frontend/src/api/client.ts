import {
  Configuration,
  InventoryApi,
  MaterialBomApi,
  ProductionApi,
  PurchaseApi,
  QualityTraceabilityApi,
  SystemApi,
} from './index'
import axios, { AxiosHeaders } from 'axios'
import { getToken } from '@/utils/storage'
import { pinia } from '@/stores/pinia'
import { useAuthStore } from '@/stores/auth'

const axiosInstance = axios.create()
let isRedirectingForForbidden = false

function handleAuthorizationStatus(status: unknown) {
  const { pathname } = globalThis.location
  const isAuthEntry = pathname === '/login.html' || pathname === '/register.html'

  if (status === 401) {
    useAuthStore(pinia).logout(!isAuthEntry)
  }

  if (status === 403 && !isAuthEntry && !isRedirectingForForbidden) {
    isRedirectingForForbidden = true
    globalThis.location.assign('/forbidden')
  }
}

axiosInstance.interceptors.request.use((config) => {
  const token = getToken()
  if (token) {
    const headers = AxiosHeaders.from(config.headers)
    headers.set('Authorization', `Bearer ${token}`)
    config.headers = headers
  }
  return config
})

axiosInstance.interceptors.response.use(
  (response) => {
    handleAuthorizationStatus((response.data as { code?: unknown } | undefined)?.code)
    return response
  },
  (error: unknown) => {
    if (!axios.isAxiosError(error)) {
      return Promise.reject(error)
    }

    handleAuthorizationStatus(error.response?.status)
    return Promise.reject(error)
  },
)

const apiConfig = new Configuration({ basePath: import.meta.env.VITE_API_BASE_URL || '' })

export interface UserData {
  createdAt?: string | null
  id: number
  name?: string | null
}

export const inventoryApi = new InventoryApi(apiConfig, apiConfig.basePath, axiosInstance)
export const materialBomApi = new MaterialBomApi(apiConfig, apiConfig.basePath, axiosInstance)
export const productionApi = new ProductionApi(apiConfig, apiConfig.basePath, axiosInstance)
export const purchaseApi = new PurchaseApi(apiConfig, apiConfig.basePath, axiosInstance)
export const qualityTraceabilityApi = new QualityTraceabilityApi(
  apiConfig,
  apiConfig.basePath,
  axiosInstance,
)
export const systemApi = new SystemApi(apiConfig, apiConfig.basePath, axiosInstance)

export const Api = {
  getUserTest: () =>
    axiosInstance.get<UserData[]>('/api/user-test', { baseURL: apiConfig.basePath }),
}
