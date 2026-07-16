<script setup lang="ts">
import type { LoginData, LoginRequest } from '@/api'
import { getRequestStatus, systemService } from '@/services/SystemService'
import { onMounted, ref } from 'vue'
import { getErrorMessage } from '@/utils/error'
import { useAuthStore } from '@/stores/auth'

const userNo = ref('')
const password = ref('')
const loading = ref(false)
const errorMessage = ref('')
const auth = useAuthStore()

function getRedirectTarget() {
  const redirect = new URLSearchParams(globalThis.location.search).get('redirect')

  if (redirect?.startsWith('/') && !redirect.startsWith('//')) {
    return redirect
  }

  return '/'
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

async function handleLoginSuccess(
  accessToken?: string,
  expiresInSeconds?: number,
  employeeNo?: string,
) {
  if (!accessToken || expiresInSeconds === undefined || expiresInSeconds <= 0) {
    errorMessage.value = '登录响应缺少令牌信息'
    return false
  }

  auth.setToken(accessToken, Math.floor(Date.now() / 1000) + expiresInSeconds)
  auth.setCurrentUser({ employeeNo })
  auth.setRoles([])
  auth.setPermissions([])
  try {
    const access = await systemService.loadCurrentAccess(employeeNo ?? '')
    auth.setCurrentUser(access.currentUser)
    auth.setRoles(access.roles)
    auth.setPermissions(access.permissions)
  } catch (error) {
    const status = getRequestStatus(error)
    if (status === 401) {
      auth.logout(false)
      errorMessage.value = '登录状态已失效，请重新登录'
      return false
    }
    if (status === 403) {
      globalThis.location.href = '/forbidden'
      return true
    }

    errorMessage.value = `登录已成功，但权限初始化失败。${getErrorMessage(error, '请稍后刷新重试')}`
    return false
  }

  globalThis.location.href = getRedirectTarget()
  return true
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
    const result = await systemService.login(loginData)
    if (!isLoginData(result)) {
      errorMessage.value = '登录响应缺少令牌信息'
      return
    }

    await handleLoginSuccess(result.access_token, result.expires, employeeNo)
  } catch (error) {
    errorMessage.value = getErrorMessage(error, '登录失败')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  if (auth.restoreSession()) {
    globalThis.location.replace('/')
  }
})
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
