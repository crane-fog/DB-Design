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
import { isMockEnabled } from '@/config/mock'
import { materialRepository } from '@/mock/repositories'

function requireMaterialMock() {
  if (!isMockEnabled()) {
    throw new Error('物料 BOM API 尚未接入；请将 VITE_DATA_MODE 设置为 mock 使用演示数据。')
  }
}

/** 物料与 BOM 页面唯一的数据入口，Mock 模式支持可持久化的前端交互闭环。 */
export const materialService = {
  addBomComponent(bomId: string, form: BomComponentForm) {
    requireMaterialMock()
    return materialRepository.addBomComponent(bomId, form)
  },
  createBomVersion(form: BomVersionForm) {
    requireMaterialMock()
    return materialRepository.createBomVersion(form)
  },
  createMaterial(form: MaterialForm) {
    requireMaterialMock()
    return materialRepository.createMaterial(form)
  },
  getBomDetail(bomId: string) {
    requireMaterialMock()
    return materialRepository.getBomDetail(bomId)
  },
  getBomSummary() {
    requireMaterialMock()
    return materialRepository.getBomSummary()
  },
  getMaterial(materialId: string) {
    requireMaterialMock()
    return materialRepository.getMaterial(materialId)
  },
  getReverseTrace(materialCode: string): Promise<BomReverseTraceResult[]> {
    requireMaterialMock()
    return materialRepository.getReverseTrace(materialCode)
  },
  getTree(bomId: string): Promise<BomTreeNode[]> {
    requireMaterialMock()
    return materialRepository.getTree(bomId)
  },
  listAnalysisHistory(): Promise<BomAnalysisRecord[]> {
    requireMaterialMock()
    return materialRepository.listAnalysisHistory()
  },
  listBomRecords(query: MaterialBomListQuery) {
    requireMaterialMock()
    return materialRepository.listBomRecords(query)
  },
  listBomVersions(materialCode: string) {
    requireMaterialMock()
    return materialRepository.listBomVersions(materialCode)
  },
  listCategories() {
    requireMaterialMock()
    return materialRepository.listCategories()
  },
  listMaterials(query: MaterialListQuery) {
    requireMaterialMock()
    return materialRepository.listMaterials(query)
  },
  removeBomComponent(bomId: string, componentId: string) {
    requireMaterialMock()
    return materialRepository.removeBomComponent(bomId, componentId)
  },
  runAnalysis(bomId: string, plannedQuantity: number): Promise<BomAnalysisResult[]> {
    requireMaterialMock()
    return materialRepository.runAnalysis(bomId, plannedQuantity)
  },
  setBomVersionReleased(bomId: string) {
    requireMaterialMock()
    return materialRepository.setBomVersionReleased(bomId)
  },
  updateBomComponent(bomId: string, form: BomComponentForm) {
    requireMaterialMock()
    return materialRepository.updateBomComponent(bomId, form)
  },
  updateMaterial(materialId: string, form: MaterialForm) {
    requireMaterialMock()
    return materialRepository.updateMaterial(materialId, form)
  },
}
