<script setup lang="ts">
import type { LoginData, LoginRequest } from '@/api'
import { ref } from 'vue'
import { systemApi } from '@/api/client'

const userNo = ref('')
const password = ref('')
const loading = ref(false)
const errorMessage = ref('')

function getRedirectTarget() {
  const redirect = new URLSearchParams(globalThis.location.search).get('redirect')

  if (redirect?.startsWith('/') && !redirect.startsWith('//')) {
    return redirect
  }

  return '/'
}

function getLoginErrorMessage(error: unknown) {
  if (typeof error === 'object' && error !== null && 'response' in error) {
    const axiosError = error as {
      response?: {
        data?: {
          message?: string
          msg?: string
        }
      }
    }

    return axiosError.response?.data?.message || axiosError.response?.data?.msg || '登录失败'
  }

  if (error instanceof Error) {
    return error.message
  }

  return '登录失败'
}

async function hashPassword(value: string) {
  const passwordBytes = new TextEncoder().encode(value)
  const hashBuffer = await crypto.subtle.digest('SHA-256', passwordBytes)

  return [...new Uint8Array(hashBuffer)].map((byte) => byte.toString(16).padStart(2, '0')).join('')
}

async function buildLoginData(employeeNo: string): Promise<LoginRequest> {
  return {
    employee_no: employeeNo,
    password: await hashPassword(password.value),
  }
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

function handleLoginSuccess(accessToken?: string, expiresInSeconds?: number) {
  if (!accessToken || expiresInSeconds === undefined || expiresInSeconds <= 0) {
    errorMessage.value = '登录响应缺少令牌信息'
    return
  }

  localStorage.setItem('jwt', accessToken)
  localStorage.setItem('expires', String(Math.floor(Date.now() / 1000) + expiresInSeconds))
  globalThis.location.href = getRedirectTarget()
}

async function submitLogin() {
  if (loading.value) {
    return
  }

  const employeeNo = userNo.value.trim()

  if (!employeeNo || !password.value) {
    errorMessage.value = '请输入账号和密码'
    return
  }

  loading.value = true
  errorMessage.value = ''

  try {
    const loginData = await buildLoginData(employeeNo)
    const response = await systemApi.login({ loginRequest: loginData })
    const result = response.data

    if (result.code !== 200) {
      errorMessage.value = result.message || '登录失败'
      return
    }

    if (!isLoginData(result.data)) {
      errorMessage.value = '登录响应缺少令牌信息'
      return
    }

    handleLoginSuccess(result.data.access_token, result.data.expires)
  } catch (error) {
    errorMessage.value = getLoginErrorMessage(error)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <main class="login-shell">
    <section class="login-panel" aria-labelledby="login-title">
      <div class="login-heading">
        <p class="login-system">工业制造物料管理系统</p>
        <h1 id="login-title">登录</h1>
      </div>

      <form class="login-form" @submit.prevent="submitLogin">
        <label class="form-field">
          <span>账号</span>
          <input
            v-model.trim="userNo"
            type="text"
            name="userNo"
            autocomplete="userNo"
            placeholder="请输入账号"
            :disabled="loading"
          />
        </label>

        <label class="form-field">
          <span>密码</span>
          <input
            v-model="password"
            type="password"
            name="password"
            autocomplete="current-password"
            placeholder="请输入密码"
            :disabled="loading"
          />
        </label>

        <p v-if="errorMessage" class="login-error" role="alert">{{ errorMessage }}</p>

        <button class="login-submit" type="submit" :disabled="loading">
          {{ loading ? '登录中...' : '登录' }}
        </button>
      </form>

      <a class="login-register" href="/register.html">注册</a>
    </section>
  </main>
</template>
