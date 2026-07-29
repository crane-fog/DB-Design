export type DataMode = 'api' | 'mock'
export type MockScenario = 'empty' | 'error' | 'success'
export type DashboardMockScenario = MockScenario

export function getDataMode(): DataMode {
  if (import.meta.env.VITE_DATA_MODE === 'api') {
    return 'api'
  }
  return 'mock'
}

export function isMockEnabled() {
  return getDataMode() === 'mock'
}

export function getMockScenario(): MockScenario {
  const value = import.meta.env.VITE_MOCK_SCENARIO
  if (value === 'empty' || value === 'error' || value === 'success') {
    return value
  }
  return 'success'
}

export function getDashboardMockScenario(): DashboardMockScenario {
  return getMockScenario()
}

export function isMockPersistenceEnabled() {
  return import.meta.env.VITE_MOCK_PERSIST !== 'false'
}

export function isMockAuthEnabled() {
  return import.meta.env.DEV && import.meta.env.VITE_USE_MOCK_AUTH === 'true'
}
