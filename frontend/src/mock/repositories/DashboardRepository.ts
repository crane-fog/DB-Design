import type {
  DashboardMockScenario,
  HomeDashboardData,
  SystemDashboardData,
} from '@/types/dashboard'
import { createMockStore, mockDelay } from '@/mock'
import { getHomeDashboardSeed, getSystemDashboardSeed } from '@/config/dashboard-mock'

const homeStore = createMockStore<HomeDashboardData>('dashboard-home', getHomeDashboardSeed)
const systemStore = createMockStore<SystemDashboardData>('dashboard-system', getSystemDashboardSeed)

function applyScenario<TValue extends HomeDashboardData | SystemDashboardData>(
  data: TValue,
  scenario: DashboardMockScenario,
): TValue {
  if (scenario !== 'empty') {
    return data
  }
  const emptyData = structuredClone(data)
  emptyData.recentOperations = { ...emptyData.recentOperations, items: [], total: 0 }
  if ('todos' in emptyData) {
    emptyData.todos = { ...emptyData.todos, items: [], total: 0 }
  }
  return emptyData
}

export const dashboardRepository = {
  getHomeDashboard(scenario: DashboardMockScenario) {
    return mockDelay(() => {
      if (scenario === 'error') {
        throw new Error('工作台数据加载失败，请稍后重试')
      }
      return applyScenario(homeStore.read(), scenario)
    }, 180)
  },
  getSystemDashboard(scenario: DashboardMockScenario) {
    return mockDelay(() => {
      if (scenario === 'error') {
        throw new Error('系统工作台数据加载失败，请稍后重试')
      }
      return applyScenario(systemStore.read(), scenario)
    }, 180)
  },
}
