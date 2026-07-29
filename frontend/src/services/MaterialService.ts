import type { MaterialBomListQuery } from '@/types/material'
import { isMockEnabled } from '@/config/mock'
import { materialBomMock } from '@/config/material-bom-mock'

function requireMaterialMock() {
  if (!isMockEnabled('material')) {
    throw new Error('物料 BOM API 尚未接入；开发环境请显式启用 VITE_USE_MATERIAL_MOCK。')
  }
}

/**
 * 物料 BOM 页面的唯一数据入口。当前后端未接入该页面所需接口，故使用集中 Mock；
 * 后续联调时仅替换本 Service 内部实现，页面和类型保持不变。
 */
export const materialService = {
  getBomDetail(bomId: string) {
    requireMaterialMock()
    return materialBomMock.getBomDetail(bomId)
  },
  getBomSummary() {
    requireMaterialMock()
    return materialBomMock.getBomSummary()
  },
  listBomRecords(query: MaterialBomListQuery) {
    requireMaterialMock()
    return materialBomMock.listBomRecords(query)
  },
}
