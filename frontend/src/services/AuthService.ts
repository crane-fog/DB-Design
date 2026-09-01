import type { LoginData, LoginRequest } from '@/api'
import { systemService } from '@/services/SystemService'

export interface AuthLoginResult {
  accessToken: string
  expiresInSeconds: number
}

async function hashPassword(value: string) {
  const passwordBytes = new TextEncoder().encode(value)
  const hashBuffer = await crypto.subtle.digest('SHA-256', passwordBytes)

  return [...new Uint8Array(hashBuffer)].map((byte) => byte.toString(16).padStart(2, '0')).join('')
}

function isLoginData(data: unknown): data is LoginData {
  return (
    typeof data === 'object' &&
    data !== null &&
    'access_token' in data &&
    'expires' in data &&
    typeof data.access_token === 'string' &&
    typeof data.expires === 'number'
  )
}

export const authService = {
  async initializeAccess() {
    return systemService.loadCurrentAccess()
  },

  async login(employeeNo: string, password: string): Promise<AuthLoginResult> {
    const request: LoginRequest = {
      employee_no: employeeNo,
      password: await hashPassword(password),
    }
    const response = await systemService.login(request)
    if (!isLoginData(response)) {
      throw new Error('登录响应缺少令牌信息')
    }

    return {
      accessToken: response.access_token,
      expiresInSeconds: response.expires,
    }
  },
}
