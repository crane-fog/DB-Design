<script setup lang="ts">
import { Api } from '@/api/client'
import type { DefaultApiLoginPostRequest } from '@/api'
import { ref } from 'vue'

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

async function buildLoginData(employeeNo: string): Promise<DefaultApiLoginPostRequest> {
  return {
    password: await hashPassword(password.value),
    userNo: employeeNo,
  }
}

function handleLoginSuccess(accessToken?: string, expires?: number) {
  if (!accessToken || expires === undefined) {
    errorMessage.value = '登录响应缺少令牌信息'
    return
  }

  localStorage.setItem('jwt', accessToken)
  localStorage.setItem('expires', String(expires))
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
    const response = await Api.loginPost(loginData)
    const result = response.data

    if (result.msg !== '登录成功') {
      errorMessage.value = result.msg || '登录失败'
      return
    }

    handleLoginSuccess(result.accessToken, result.expires)
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
