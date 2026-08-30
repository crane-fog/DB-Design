const TOKEN_KEY = 'jwt'
const EXPIRES_AT_KEY = 'expires'
const SESSION_KEY = 'app-auth-session'

export interface StoredSession {
  currentUser?: { id?: number; name?: string; employeeNo?: string }
  permissions: string[]
  roles: string[]
}

export function getToken() {
  return localStorage.getItem(TOKEN_KEY)
}

export function getTokenExpiresAt() {
  const expiresAt = Number(localStorage.getItem(EXPIRES_AT_KEY))
  if (Number.isFinite(expiresAt)) {
    return expiresAt
  }
  return undefined
}

export function setToken(token: string, expiresAt: number) {
  localStorage.setItem(TOKEN_KEY, token)
  localStorage.setItem(EXPIRES_AT_KEY, String(expiresAt))
}

export function clearToken() {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(EXPIRES_AT_KEY)
}

export function getStoredSession(): StoredSession | undefined {
  const value = localStorage.getItem(SESSION_KEY)
  if (!value) {
    return undefined
  }

  try {
    return JSON.parse(value) as StoredSession
  } catch {
    localStorage.removeItem(SESSION_KEY)
    return undefined
  }
}

export function setStoredSession(session: StoredSession) {
  localStorage.setItem(SESSION_KEY, JSON.stringify(session))
}

export function clearStoredSession() {
  localStorage.removeItem(SESSION_KEY)
}
