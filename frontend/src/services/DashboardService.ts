import { getDashboardMockScenario, isMockEnabled } from '@/config/mock'
import { dashboardRepository } from '@/mock/repositories'

/** 工作台数据的唯一入口。当前工作台数据由集中 Mock 提供。 */
export const dashboardService = {
  getHomeDashboard() {
    if (!isMockEnabled()) {
      throw new Error('工作台当前仅提供 Mock 数据，请将 VITE_DATA_MODE 设置为 mock。')
    }
    return dashboardRepository.getHomeDashboard(getDashboardMockScenario())
  },
  getSystemDashboard() {
    if (!isMockEnabled()) {
      throw new Error('系统工作台当前仅提供 Mock 数据，请将 VITE_DATA_MODE 设置为 mock。')
    }
    return dashboardRepository.getSystemDashboard(getDashboardMockScenario())
  },
}
