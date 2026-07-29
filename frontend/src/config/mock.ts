export type DashboardMockScenario = 'empty' | 'error' | 'success'
export type MockModule = 'dashboard' | 'inventory' | 'material' | 'purchase'

const mockFlags: Record<MockModule, string> = {
  dashboard: 'VITE_USE_DASHBOARD_MOCK',
  inventory: 'VITE_USE_INVENTORY_MOCK',
  material: 'VITE_USE_MATERIAL_MOCK',
  purchase: 'VITE_USE_PURCHASE_MOCK',
}

function readMockFlag(flag: string) {
  return import.meta.env.DEV && import.meta.env[flag] === 'true'
}

export function isMockEnabled(module: MockModule) {
  return readMockFlag(mockFlags[module])
}

export function getDashboardMockScenario(): DashboardMockScenario {
  const value = import.meta.env.VITE_DASHBOARD_MOCK_SCENARIO
  if (value === 'empty' || value === 'error' || value === 'success') {
    return value
  }
  return 'success'
}
