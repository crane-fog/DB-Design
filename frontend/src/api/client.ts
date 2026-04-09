// src/api/client.ts
import { Configuration, DefaultApi } from './index'
import axios from 'axios'

const axiosInstance = axios.create()

const apiConfig = new Configuration({
  basePath: '/api',
})

export const Api = new DefaultApi(apiConfig, apiConfig.basePath, axiosInstance)
