<script setup lang="ts">
import { type AuthLoginResult, authService } from '@/services/AuthService'
import { onMounted, ref } from 'vue'
import { getErrorMessage } from '@/utils/error'
import { getRequestStatus } from '@/services/SystemService'
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

async function handleLoginSuccess(result: AuthLoginResult, employeeNo: string) {
  const { accessToken, expiresInSeconds } = result
  if (!accessToken || expiresInSeconds === undefined || expiresInSeconds <= 0) {
    errorMessage.value = '登录响应缺少令牌信息'
    return false
  }

  auth.setToken(accessToken, Math.floor(Date.now() / 1000) + expiresInSeconds)
  auth.setAccess({ currentUser: { employeeNo }, permissions: [], roles: [] })
  try {
    const access = await authService.initializeAccess()
    auth.setAccess(access)
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
    const result = await authService.login(employeeNo, password.value)
    await handleLoginSuccess(result, employeeNo)
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
