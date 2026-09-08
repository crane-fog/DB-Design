import {
  clearStoredSession,
  clearToken,
  getStoredSession,
  getToken,
  getTokenExpiresAt,
  setToken as saveToken,
  setStoredSession,
} from '@/utils/storage'
import type { PermissionCode, RoleBrief } from '@/api'
import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

export interface CurrentUser {
  employeeNo?: string
  id?: number
  name?: string
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string>()
  const expiresAt = ref<number>()
  const currentUser = ref<CurrentUser>()
  const roles = ref<RoleBrief[]>([])
  const permissions = ref<PermissionCode[]>([])

  const isTokenExpired = computed(() => !expiresAt.value || Date.now() >= expiresAt.value * 1000)
  const isAuthenticated = computed(() => Boolean(token.value) && !isTokenExpired.value)

  function persistSession() {
    setStoredSession({
      currentUser: currentUser.value,
      permissions: permissions.value,
      roles: roles.value,
    })
  }

  function setToken(value: string, expiresAtSeconds: number) {
    token.value = value
    expiresAt.value = expiresAtSeconds
    saveToken(value, expiresAtSeconds)
  }

  function restoreSession() {
    const storedToken = getToken()
    const storedExpiresAt = getTokenExpiresAt()
    if (!storedToken || !storedExpiresAt || Date.now() >= storedExpiresAt * 1000) {
      logout(false)
      return false
    }

    token.value = storedToken
    expiresAt.value = storedExpiresAt
    const session = getStoredSession()
    currentUser.value = session?.currentUser
    roles.value = session?.roles ?? []
    permissions.value = session?.permissions ?? []
    return true
  }

  function logout(redirect = true) {
    token.value = undefined
    expiresAt.value = undefined
    currentUser.value = undefined
    roles.value = []
    permissions.value = []
    clearToken()
    clearStoredSession()

    if (redirect && globalThis.location.pathname !== '/login.html') {
      const redirectTarget = `${globalThis.location.pathname}${globalThis.location.search}${globalThis.location.hash}`
      globalThis.location.assign(`/login.html?redirect=${encodeURIComponent(redirectTarget)}`)
    }
  }

  function hasPermission(permission: PermissionCode) {
    return permissions.value.includes(permission)
  }

  function hasAnyPermission(...values: PermissionCode[]) {
    return values.some((permission) => hasPermission(permission))
  }

  function setAccess(value: {
    currentUser: CurrentUser
    permissions: PermissionCode[]
    roles: RoleBrief[]
  }) {
    currentUser.value = value.currentUser
    permissions.value = value.permissions
    roles.value = value.roles
    persistSession()
  }

  return {
    currentUser,
    expiresAt,
    hasAnyPermission,
    hasPermission,
    isAuthenticated,
    isTokenExpired,
    logout,
    permissions,
    restoreSession,
    roles,
    setAccess,
    setToken,
    token,
  }
})
