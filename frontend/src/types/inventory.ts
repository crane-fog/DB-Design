export type InventoryAlertStatus = 'handled' | 'ignored' | 'pending'
export type StockLockStatus = 'cancelled' | 'consumed' | 'locked'
export type ObsoleteMaterialStatus = 'handled' | 'ignored' | 'pending'
export type InventoryMaterialType = 'auxiliary' | 'finished' | 'raw_material' | 'semi_finished'
export type InventoryStockStatus = 'locked' | 'low' | 'normal' | 'zero'

export interface InventoryStockItem {
  availableQty: number
  lastInDate?: string
  lastOutDate?: string
  lockedQty: number
  materialId: number
  materialName: string
  materialType: InventoryMaterialType
  safetyStock: number
  status: InventoryStockStatus
  unit?: string
}

export interface InventoryStockQuery {
  materialId?: number
  materialName?: string
  materialType?: InventoryMaterialType
  page: number
  pageSize: number
  status?: InventoryStockStatus
}

export interface InventoryMaterialOption {
  materialId: number
  materialName: string
  materialType: InventoryMaterialType
  unit?: string
}

export interface InventoryBomVersionOption {
  materialId: number
  versionId: number
  versionNo: string
}

export interface InventoryProductionOrderOption {
  finishedQty: number
  materialId: number
  materialName: string
  orderId: number
  planQty: number
  remainingQty: number
  status: string
  versionId: number
  versionNo?: string
}

export interface InventoryReferenceData {
  bomVersions: InventoryBomVersionOption[]
  materials: InventoryMaterialOption[]
  productionOrders: InventoryProductionOrderOption[]
}

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

export interface InventoryAlertDetail extends InventoryAlertItem {
  recommendedAction: string
  stock: InventoryStockItem
}

export interface InventoryAlertQuery extends PageRequest {
  endTime?: string
  materialId?: number
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

export interface StockLockQuery extends PageRequest {
  materialId?: number
  orderId?: number
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
  activeOrderIds?: number[]
  availableQty: number
  bomVersionIds?: number[]
  detectTime: string
  detectionId: number
  handlerId?: number
  idleDays: number
  lastOutDate?: string
  materialId: number
  materialName?: string
  status: ObsoleteMaterialStatus
}

export interface ObsoleteMaterialDetail extends ObsoleteMaterialItem {
  activeOrders: {
    materialName: string
    orderId: number
    status: string
  }[]
  bomVersions: {
    versionId: number
    versionNo: string
  }[]
  stock: InventoryStockItem
}

export interface ObsoleteDetectionResult {
  detectedCount: number
  items: ObsoleteMaterialItem[]
}

export interface ObsoleteMaterialQuery extends PageRequest {
  endTime?: string
  materialId?: number
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
  operatorName?: string
  orderId: number
  productName?: string
  qualifiedQty: number
  versionId: number
}

export interface CompletionInboundDetail extends CompletionInboundItem {
  bomVersionNo?: string
  productionOrder?: {
    materialName: string
    orderId: number
    status: string
  }
}

export interface CompletionInboundQuery extends PageRequest {
  endTime?: string
  materialId?: number
  orderId?: number
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
  availableMaterialCount: number
  inboundCount: number
  lowStockCount: number
  lockedCount: number
  lockedMaterialCount: number
  materialCount: number
  obsoletePendingCount: number
  pendingAlertCount: number
  zeroStockCount: number
}
import type { PageRequest } from '@/services/pagination'
