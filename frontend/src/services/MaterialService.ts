import type {
  BomAnalysisRecord,
  BomAnalysisResult,
  BomComponentForm,
  BomReverseTraceResult,
  BomTreeNode,
  BomVersionForm,
  MaterialBomListQuery,
  MaterialForm,
  MaterialListQuery,
} from '@/types/material'
import { materialBomMock } from '@/config/material-bom-mock'

/**
 * 物料与 BOM 页面唯一的数据入口。当前页面使用可变 Mock 完成前端交互闭环；
 * 后续接入接口时，只需在此处替换实现，页面和类型无需变更。
 */
export const materialService = {
  addBomComponent(bomId: string, form: BomComponentForm) {
    return materialBomMock.addBomComponent(bomId, form)
  },
  createBomVersion(form: BomVersionForm) {
    return materialBomMock.createBomVersion(form)
  },
  createMaterial(form: MaterialForm) {
    return materialBomMock.createMaterial(form)
  },
  getBomDetail(bomId: string) {
    return materialBomMock.getBomDetail(bomId)
  },
  getBomSummary() {
    return materialBomMock.getBomSummary()
  },
  getMaterial(materialId: string) {
    return materialBomMock.getMaterial(materialId)
  },
  getReverseTrace(materialCode: string): Promise<BomReverseTraceResult[]> {
    return materialBomMock.getReverseTrace(materialCode)
  },
  getTree(bomId: string): Promise<BomTreeNode[]> {
    return materialBomMock.getTree(bomId)
  },
  listAnalysisHistory(): Promise<BomAnalysisRecord[]> {
    return materialBomMock.listAnalysisHistory()
  },
  listBomRecords(query: MaterialBomListQuery) {
    return materialBomMock.listBomRecords(query)
  },
  listBomVersions(materialCode: string) {
    return materialBomMock.listBomVersions(materialCode)
  },
  listCategories() {
    return materialBomMock.listCategories()
  },
  listMaterials(query: MaterialListQuery) {
    return materialBomMock.listMaterials(query)
  },
  removeBomComponent(bomId: string, componentId: string) {
    return materialBomMock.removeBomComponent(bomId, componentId)
  },
  runAnalysis(bomId: string, plannedQuantity: number): Promise<BomAnalysisResult[]> {
    return materialBomMock.runAnalysis(bomId, plannedQuantity)
  },
  setBomVersionReleased(bomId: string) {
    return materialBomMock.setBomVersionReleased(bomId)
  },
  updateBomComponent(bomId: string, form: BomComponentForm) {
    return materialBomMock.updateBomComponent(bomId, form)
  },
  updateMaterial(materialId: string, form: MaterialForm) {
    return materialBomMock.updateMaterial(materialId, form)
  },
}
