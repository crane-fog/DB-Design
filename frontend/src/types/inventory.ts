export type InventoryAlertStatus = 'handled' | 'ignored' | 'pending'
export type StockLockStatus = 'cancelled' | 'consumed' | 'locked'
export type ObsoleteMaterialStatus = 'handled' | 'ignored' | 'pending'

export interface MaterialShortageRequestItem {
  materialId: number
  productionQty: number
  versionId: number
}

export interface MaterialShortageItem {
  availableQty: number
  grossRequirement: number
  inTransitQty: number
  level: number
  materialId: number
  materialName?: string
  netShortageQty: number
  parentMaterialId?: number
  safetyStock: number
  suggestedPurchaseQty: number
}

export interface MaterialShortageResult {
  calculatedAt: string
  items: MaterialShortageItem[]
}

export interface InventoryAlertItem {
  alertId: number
  alertTime: string
  alertType: 'low_stock'
  availableQty: number
  handleTime?: string
  handlerId?: number
  materialId: number
  materialName?: string
  status: InventoryAlertStatus
  threshold: number
}

export interface InventoryAlertQuery {
  endTime?: string
  materialId?: number
  page: number
  pageSize: number
  startTime?: string
  status?: InventoryAlertStatus
}

export interface InventoryAlertGenerateResult {
  generatedCount: number
  items: InventoryAlertItem[]
  skippedPendingCount: number
}

export interface StockLockItem {
  lockId: number
  lockQty: number
  lockTime: string
  materialId: number
  materialName?: string
  operatorId: number
  orderId: number
  releaseTime?: string
  status: StockLockStatus
}

export interface StockLockQuery {
  materialId?: number
  orderId?: number
  page: number
  pageSize: number
  status?: StockLockStatus
}

export interface StockLockFormData {
  items: { lockQty: number; materialId: number }[]
  operatorId: number
  orderId: number
}

export interface StockLockResult {
  items: StockLockItem[]
  shortages: {
    availableQty: number
    materialId: number
    requiredQty: number
    shortageQty: number
  }[]
  success: boolean
}

export interface ObsoleteMaterialItem {
  availableQty: number
  detectTime: string
  detectionId: number
  handlerId?: number
  idleDays: number
  lastOutDate?: string
  materialId: number
  materialName?: string
  status: ObsoleteMaterialStatus
}

export interface ObsoleteDetectionResult {
  detectedCount: number
  items: ObsoleteMaterialItem[]
}

export interface ObsoleteMaterialQuery {
  endTime?: string
  materialId?: number
  page: number
  pageSize: number
  startTime?: string
  status?: ObsoleteMaterialStatus
}

export interface CompletionInboundItem {
  batchNo: string
  consumedLockRecords?: StockLockItem[]
  finishQty: number
  inboundId: number
  inboundTime: string
  materialId: number
  operatorId: number
  orderId: number
  productName?: string
  qualifiedQty: number
  versionId: number
}

export interface CompletionInboundQuery {
  endTime?: string
  materialId?: number
  orderId?: number
  page: number
  pageSize: number
  startTime?: string
}

export interface CompletionInboundFormData {
  batchNo: string
  finishQty: number
  materialId: number
  operatorId: number
  orderId: number
  qualifiedQty: number
  versionId: number
}

export interface InventoryOverviewSummary {
  inboundCount: number
  lockedCount: number
  obsoletePendingCount: number
  pendingAlertCount: number
}
