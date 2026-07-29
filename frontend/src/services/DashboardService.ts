import { getDashboardMockScenario, isMockEnabled } from '@/config/mock'
import { dashboardMock } from '@/config/dashboard-mock'

/**
 * 工作台数据的唯一入口。当前 OpenAPI 契约没有工作台接口，故使用集中 Mock；
 * 后端接口就绪后在本 Service 内替换实现，页面和类型无需变更。
 */
export const dashboardService = {
  getHomeDashboard() {
    if (!isMockEnabled()) {
      throw new Error('工作台 API 尚未接入；请将 VITE_DATA_MODE 设置为 mock 使用演示数据。')
    }
    return dashboardMock.getHomeDashboard(getDashboardMockScenario())
  },
  getSystemDashboard() {
    if (!isMockEnabled()) {
      throw new Error('工作台 API 尚未接入；请将 VITE_DATA_MODE 设置为 mock 使用演示数据。')
    }
    return dashboardMock.getSystemDashboard(getDashboardMockScenario())
  },
}
