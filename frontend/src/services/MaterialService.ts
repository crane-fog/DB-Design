import type { MaterialBomListQuery } from '@/types/material'
import { materialBomMock } from '@/config/material-bom-mock'

/**
 * 物料 BOM 页面的唯一数据入口。当前后端未接入该页面所需接口，故使用集中 Mock；
 * 后续联调时仅替换本 Service 内部实现，页面和类型保持不变。
 */
export const materialService = {
  getBomDetail(bomId: string) {
    return materialBomMock.getBomDetail(bomId)
  },
  getBomSummary() {
    return materialBomMock.getBomSummary()
  },
  listBomRecords(query: MaterialBomListQuery) {
    return materialBomMock.listBomRecords(query)
  },
}
