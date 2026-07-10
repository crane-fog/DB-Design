// src/api/client.ts
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

const axiosInstance = axios.create()

axiosInstance.interceptors.request.use((config) => {
  const token = localStorage.getItem('jwt')

  if (token) {
    const headers = AxiosHeaders.from(config.headers)
    headers.set('Authorization', `Bearer ${token}`)
    config.headers = headers
  }

  return config
})

const apiConfig = new Configuration({
  basePath: '',
})

export interface UserData {
  id: number
  name?: string | null
  createdAt?: string | null
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
  getUserTest() {
    return axiosInstance.get<UserData[]>('/api/user-test')
  },
}
