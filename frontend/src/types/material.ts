export type MaterialBomStatus = 'archived' | 'draft' | 'released'

export type MaterialBomComponentType = 'material' | 'semiFinished'

export interface MaterialBomListQuery {
  keyword?: string
  owner?: string
  page: number
  pageSize: number
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
