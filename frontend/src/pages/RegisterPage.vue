<script setup lang="ts">
import { type RegisterFormData, systemService } from '@/services/SystemService'
import { getErrorMessage } from '@/utils/error'
import { ref } from 'vue'

const userNo = ref('')
const password = ref('')
const confirmPassword = ref('')
const userName = ref('')
const phone = ref('')
const email = ref('')
const loading = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

function validateForm() {
  if (!userNo.value.trim() || !password.value || !userName.value.trim() || !phone.value.trim()) {
    return '请填写必填项'
  }

  if (password.value !== confirmPassword.value) {
    return '两次输入的密码不一致'
  }

  return ''
}

function buildRegisterData(): RegisterFormData {
  return {
    email: email.value,
    employeeNo: userNo.value,
    password: password.value,
    phone: phone.value,
    userName: userName.value,
  }
}

async function submitRegister() {
  if (loading.value) {
    return
  }

  const validationError = validateForm()
  if (validationError) {
    errorMessage.value = validationError
    return
  }

  loading.value = true
  errorMessage.value = ''
  successMessage.value = ''

  try {
    await systemService.register(buildRegisterData())

    successMessage.value = '注册成功'
    password.value = ''
    confirmPassword.value = ''
    setTimeout(() => {
      globalThis.location.href = '/login.html'
    }, 2000)
  } catch (error) {
    errorMessage.value = getErrorMessage(error, '注册失败')
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <main class="login-shell">
    <section class="login-panel register-panel" aria-labelledby="register-title">
      <div class="login-heading">
        <p class="login-system">工业制造物料管理系统</p>
        <h1 id="register-title">注册</h1>
      </div>

      <form class="login-form" @submit.prevent="submitRegister">
        <label class="form-field">
          <span>账号</span>
          <input
            v-model.trim="userNo"
            type="text"
            name="userNo"
            autocomplete="username"
            placeholder="请输入账号"
            :disabled="loading"
          />
        </label>

        <label class="form-field">
          <span>姓名</span>
          <input
            v-model.trim="userName"
            type="text"
            name="userName"
            placeholder="请输入姓名"
            :disabled="loading"
          />
        </label>

        <label class="form-field">
          <span>手机号</span>
          <input
            v-model.trim="phone"
            type="tel"
            name="phone"
            autocomplete="tel"
            placeholder="请输入手机号"
            :disabled="loading"
          />
        </label>

        <label class="form-field">
          <span>邮箱</span>
          <input
            v-model.trim="email"
            type="email"
            name="email"
            autocomplete="email"
            placeholder="可选"
            :disabled="loading"
          />
        </label>

        <label class="form-field">
          <span>密码</span>
          <input
            v-model="password"
            type="password"
            name="password"
            autocomplete="new-password"
            placeholder="请输入密码"
            :disabled="loading"
          />
        </label>

        <label class="form-field">
          <span>确认密码</span>
          <input
            v-model="confirmPassword"
            type="password"
            name="confirmPassword"
            autocomplete="new-password"
            placeholder="请再次输入密码"
            :disabled="loading"
          />
        </label>

        <p v-if="errorMessage" class="login-error" role="alert">{{ errorMessage }}</p>
        <p v-if="successMessage" class="login-success" role="status">{{ successMessage }}</p>

        <button class="login-submit" type="submit" :disabled="loading">
          {{ loading ? '注册中...' : '注册' }}
        </button>
      </form>

      <a class="login-register" href="/login.html">返回登录</a>
    </section>
  </main>
</template>
