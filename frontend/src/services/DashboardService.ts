import type { DashboardMockScenario } from '@/types/dashboard'
import { dashboardMock } from '@/config/dashboard-mock'

function getMockScenario(): DashboardMockScenario {
  const configuredScenario = import.meta.env.VITE_DASHBOARD_MOCK_SCENARIO
  if (
    configuredScenario === 'empty' ||
    configuredScenario === 'error' ||
    configuredScenario === 'success'
  ) {
    return configuredScenario
  }
  return 'success'
}

/**
 * 工作台数据的唯一入口。当前 OpenAPI 契约没有工作台接口，故使用集中 Mock；
 * 后端接口就绪后在本 Service 内替换实现，页面和类型无需变更。
 */
export const dashboardService = {
  getHomeDashboard() {
    return dashboardMock.getHomeDashboard(getMockScenario())
  },
  getSystemDashboard() {
    return dashboardMock.getSystemDashboard(getMockScenario())
  },
}
