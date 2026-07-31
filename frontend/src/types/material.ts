import type { PageRequest } from '@/services/pagination'

export type MaterialBomStatus = 'archived' | 'draft' | 'released'
export type MaterialBomComponentType = 'material' | 'semiFinished'
export type MaterialStatus = 'active' | 'disabled'
export type MaterialType = 'finished' | 'raw' | 'semiFinished'

export interface MaterialBomListQuery extends PageRequest {
  keyword?: string
  owner?: string
  status?: MaterialBomStatus
}

export interface MaterialBomSummary {
  activeCount: number
  archivedCount: number
  draftCount: number
  releasedCount: number
}

export interface MaterialBomListItem {
  bomCode: string
  bomId: string
  componentCount: number
  effectiveDate: string
  materialCode: string
  materialName: string
  owner: string
  status: MaterialBomStatus
  totalLossRate: number
  totalQuantity: number
  unit: string
  updatedAt: string
  version: string
}

export interface MaterialBomComponent {
  componentId: string
  leadTimeDays: number
  lineNo: number
  lossRate: number
  materialCode: string
  materialName: string
  quantity: number
  remark?: string
  substituteGroup?: string
  type: MaterialBomComponentType
  unit: string
  workCenter: string
}

export interface MaterialBomAudit {
  action: string
  operator: string
  operatedAt: string
}

export interface MaterialBomDetail extends MaterialBomListItem {
  audits: MaterialBomAudit[]
  components: MaterialBomComponent[]
  description: string
}

export interface MaterialCategory {
  id: string
  name: string
}

export interface MaterialRecord {
  categoryId: string
  categoryName: string
  code: string
  createdAt: string
  currentBomVersion?: string
  id: string
  model: string
  name: string
  status: MaterialStatus
  type: MaterialType
  unit: string
  updatedAt: string
}

export interface MaterialListQuery extends PageRequest {
  categoryId?: string
  createdFrom?: string
  createdTo?: string
  keyword?: string
  status?: MaterialStatus
  type?: MaterialType
}

export type MaterialForm = Pick<
  MaterialRecord,
  'categoryId' | 'code' | 'model' | 'name' | 'status' | 'type' | 'unit'
>

export interface BomComponentForm {
  componentId?: string
  lossRate: number
  materialCode: string
  quantity: number
}

export interface BomVersionForm {
  effectiveDate: string
  materialCode: string
  reason: string
  version: string
}

export interface BomTreeNode {
  children: BomTreeNode[]
  cumulativeQuantity: number
  isLeaf: boolean
  level: number
  lossRate?: number
  materialCode: string
  materialName: string
  path: string
  quantity?: number
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
  cumulativeQuantity: number
  lossRate: number
  materialCode: string
  materialName: string
  path: string
  theoreticalQuantity: number
  unit: string
  withLossQuantity: number
}

export interface BomReverseTraceResult {
  cumulativeQuantity: number
  finalMaterialCode: string
  level: number
  materialCode: string
  materialName: string
  parentMaterialCode: string
  path: string
  unit: string
  version: string
}
