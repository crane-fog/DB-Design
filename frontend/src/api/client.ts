// src/api/client.ts
import { Configuration, DefaultApi } from './index'
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
  basePath: '/',
})

export const Api = new DefaultApi(apiConfig, apiConfig.basePath, axiosInstance)
