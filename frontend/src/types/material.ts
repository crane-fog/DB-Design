import type { PageRequest } from '@/services/pagination'

export type MaterialType = 'auxiliary' | 'finished' | 'raw' | 'semiFinished'

export interface MaterialCategory {
  id: string
  name: string
}

export interface MaterialRecord {
  categoryId: string
  categoryName: string
  /** 后端自动分配的 material_id，不是独立的业务编码。 */
  code: string
  createdAt: string
  currentBomVersion?: string
  currentVersionId?: number | null
  defaultSupplierId?: number | null
  id: string
  model: string
  name: string
  safetyStock: number
  type: MaterialType
  unit: string
  updatedAt: string
}

export interface MaterialListQuery extends PageRequest {
  categoryId?: string
  createdFrom?: string
  createdTo?: string
  keyword?: string
  type?: MaterialType
}

export type MaterialForm = Pick<MaterialRecord, 'categoryId' | 'model' | 'name' | 'type' | 'unit'>

export interface MaterialBomListItem {
  /** 页面选择的是版本，值对应 version_id；明细 ID 单独存放在 componentId。 */
  bomId: string
  effectiveDate: string
  expireDate?: string
  isCurrent: boolean
  materialCode: string
  materialName: string
  version: string
}

export interface MaterialBomComponent {
  componentId: string
  lineNo: number
  /** 页面使用百分数，服务层负责与接口的 0～1 小数转换。 */
  lossRate: number
  materialCode: string
  materialName: string
  quantity: number
  unit: string
}

export interface MaterialBomDetail extends MaterialBomListItem {
  components: MaterialBomComponent[]
  description: string
}

export interface BomComponentForm {
  componentId?: string
  lossRate: number
  materialCode: string
  quantity: number
}

export interface BomVersionForm {
  effectiveDate: string
  expireDate: string
  materialCode: string
  reason: string
  version: string
}

export interface BomTreeNode {
  children: BomTreeNode[]
  cumulativeQuantity: number
  isLeaf: boolean
  level: number
  materialCode: string
  materialName: string
  path: string
  quantity: number
  unit: string
}

export interface BomAnalysisRecord {
  bomId: string
  executedAt: string
  id: string
  materialCode: string
  materialName: string
  plannedQuantity: number
  version: string
}

export interface BomAnalysisResult {
  materialCode: string
  materialName: string
  path: string
  theoreticalQuantity: number
  unit: string
  withLossQuantity: number
}

export interface BomReverseTraceResult {
  cumulativeQuantity: number
  level: number
  path: string
  productMaterialCode: string
  productMaterialName: string
  unit: string
  version: string
  versionId: string
  versionStatus: 'effective' | 'history'
}
