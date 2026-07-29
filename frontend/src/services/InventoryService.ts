import {
  type ApiEnvelope,
  type PageResult,
  mapPageResult,
  optionalText,
  unwrap,
} from '@/services/pagination'
import type {
  MaterialShortageItem as ApiMaterialShortageItem,
  CompletionInboundOrder,
  InventoryAlertEvent,
  ObsoleteMaterialDetection,
  StockLockRecord,
} from '@/api'
import type {
  CompletionInboundFormData,
  CompletionInboundItem,
  CompletionInboundQuery,
  InventoryAlertGenerateResult,
  InventoryAlertItem,
  InventoryAlertQuery,
  InventoryOverviewSummary,
  MaterialShortageRequestItem,
  MaterialShortageResult,
  ObsoleteDetectionResult,
  ObsoleteMaterialItem,
  ObsoleteMaterialQuery,
  StockLockFormData,
  StockLockItem,
  StockLockQuery,
  StockLockResult,
} from '@/types/inventory'
import { cleanQuery } from '@/services/request'
import { inventoryApi } from '@/api/client'
import { inventoryMock } from '@/config/inventory-mock'
import { isMockEnabled } from '@/config/mock'

export type { PageResult }

interface ShortagePayload {
  calculation_time?: string
  records?: ApiMaterialShortageItem[]
}

interface AlertGeneratePayload {
  generated_count?: number
  records?: InventoryAlertEvent[]
  skipped_pending_count?: number
}

interface LockPayload {
  records?: StockLockRecord[]
  shortages?: {
    available_qty: number
    material_id: number
    required_qty: number
    shortage_qty: number
  }[]
  success?: boolean
}

interface ObsoleteDetectPayload {
  detected_count?: number
  records?: ObsoleteMaterialDetection[]
}

function mapOptional<TSource, TResult>(
  value: TSource | undefined,
  mapper: (item: TSource) => TResult,
) {
  if (value === undefined) {
    return undefined
  }
  return mapper(value)
}

function toAlert(item: InventoryAlertEvent): InventoryAlertItem {
  return {
    alertId: item.alert_id,
    alertTime: item.alert_time,
    alertType: item.alert_type,
    availableQty: item.available_qty,
    handleTime: optionalText(item.handle_time),
    handlerId: item.handler_id ?? undefined,
    materialId: item.material_id,
    materialName: optionalText(item.material_name),
    status: item.status,
    threshold: item.threshold,
  }
}

function toLock(item: StockLockRecord): StockLockItem {
  return {
    lockId: item.lock_id,
    lockQty: item.lock_qty,
    lockTime: item.lock_time,
    materialId: item.material_id,
    materialName: optionalText(item.material_name),
    operatorId: item.operator_id,
    orderId: item.order_id,
    releaseTime: optionalText(item.release_time),
    status: item.status,
  }
}

function toObsolete(item: ObsoleteMaterialDetection): ObsoleteMaterialItem {
  return {
    availableQty: item.available_qty,
    detectTime: item.detect_time,
    detectionId: item.detection_id,
    handlerId: item.handler_id ?? undefined,
    idleDays: item.idle_days,
    lastOutDate: optionalText(item.last_out_date),
    materialId: item.material_id,
    materialName: optionalText(item.material_name),
    status: item.status,
  }
}

function toInbound(item: CompletionInboundOrder): CompletionInboundItem {
  return {
    batchNo: item.batch_no,
    consumedLockRecords: item.consumed_lock_records?.map(toLock),
    finishQty: item.finish_qty,
    inboundId: item.inbound_id,
    inboundTime: item.inbound_time,
    materialId: item.material_id,
    operatorId: item.operator_id,
    orderId: item.order_id,
    productName: optionalText(item.product_name),
    qualifiedQty: item.qualified_qty,
    versionId: item.version_id,
  }
}

function toShortage(item: ApiMaterialShortageItem) {
  return {
    availableQty: item.available_qty,
    grossRequirement: item.gross_requirement,
    inTransitQty: item.in_transit_qty,
    level: item.level,
    materialId: item.material_id,
    materialName: optionalText(item.material_name),
    netShortageQty: item.net_shortage_qty,
    parentMaterialId: item.parent_material_id ?? undefined,
    safetyStock: item.safety_stock,
    suggestedPurchaseQty: item.suggested_purchase_qty ?? item.net_shortage_qty,
  }
}

function toIsoDayBoundary(value: string | undefined, endOfDay: boolean) {
  if (!value || value.length > 10) {
    return value
  }
  let time = '00:00:00.000'
  if (endOfDay) {
    time = '23:59:59.999'
  }
  const parsed = new Date(`${value}T${time}`)
  if (Number.isNaN(parsed.getTime())) {
    return value
  }
  return parsed.toISOString()
}

export const inventoryService = {
  async addCompletionInbound(form: CompletionInboundFormData) {
    if (isMockEnabled()) {
      return inventoryMock.addCompletionInbound(form)
    }
    const response = await inventoryApi.addCompletionInbound({
      completionInboundCreateRequest: {
        batch_no: form.batchNo,
        finish_qty: form.finishQty,
        material_id: form.materialId,
        operator_id: form.operatorId,
        order_id: form.orderId,
        qualified_qty: form.qualifiedQty,
        version_id: form.versionId,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<CompletionInboundOrder | undefined>)
    return mapOptional(data, toInbound)
  },

  async calculateShortage(items: MaterialShortageRequestItem[]): Promise<MaterialShortageResult> {
    if (isMockEnabled()) {
      return inventoryMock.calculateShortage(items)
    }
    const response = await inventoryApi.calculateMaterialShortage({
      materialShortageCalculateRequest: {
        items: items.map((item) => ({
          material_id: item.materialId,
          production_qty: item.productionQty,
          version_id: item.versionId,
        })),
      },
    })
    const data = unwrap(response.data as ApiEnvelope<ShortagePayload | undefined>)
    return {
      calculatedAt: data?.calculation_time ?? new Date().toISOString(),
      items: (data?.records ?? []).map(toShortage),
    }
  },

  async detectObsolete(
    idleDaysThreshold: number,
    materialId?: number,
  ): Promise<ObsoleteDetectionResult> {
    if (isMockEnabled()) {
      return inventoryMock.detectObsolete(idleDaysThreshold, materialId)
    }
    const response = await inventoryApi.detectObsoleteMaterial({
      obsoleteMaterialDetectRequest: {
        idle_days_threshold: idleDaysThreshold,
        material_id: materialId,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<ObsoleteDetectPayload | undefined>)
    const items = (data?.records ?? []).map(toObsolete)
    return {
      detectedCount: data?.detected_count ?? items.length,
      items,
    }
  },

  async generateAlerts(materialId?: number): Promise<InventoryAlertGenerateResult> {
    if (isMockEnabled()) {
      return inventoryMock.generateAlerts(materialId)
    }
    let request: { material_id: number } | undefined = undefined
    if (materialId !== undefined) {
      request = { material_id: materialId }
    }
    const response = await inventoryApi.generateInventoryAlert({
      inventoryAlertGenerateRequest: request,
    })
    const data = unwrap(response.data as ApiEnvelope<AlertGeneratePayload | undefined>)
    return {
      generatedCount: data?.generated_count ?? 0,
      items: (data?.records ?? []).map(toAlert),
      skippedPendingCount: data?.skipped_pending_count ?? 0,
    }
  },

  async getOverview(): Promise<InventoryOverviewSummary> {
    const [alerts, locks, obsolete, inbound] = await Promise.all([
      this.listAlerts({ page: 1, pageSize: 1, status: 'pending' }),
      this.listLocks({ page: 1, pageSize: 1, status: 'locked' }),
      this.listObsolete({ page: 1, pageSize: 1, status: 'pending' }),
      this.listCompletionInbound({ page: 1, pageSize: 1 }),
    ])
    return {
      inboundCount: inbound.total,
      lockedCount: locks.total,
      obsoletePendingCount: obsolete.total,
      pendingAlertCount: alerts.total,
    }
  },

  async handleAlert(alertId: number, status: 'handled' | 'ignored', handlerId: number) {
    if (isMockEnabled()) {
      return inventoryMock.handleAlert(alertId, status, handlerId)
    }
    const response = await inventoryApi.handleInventoryAlert({
      inventoryAlertHandleRequest: { alert_id: alertId, handler_id: handlerId, status },
    })
    const data = unwrap(response.data as ApiEnvelope<InventoryAlertEvent | undefined>)
    return mapOptional(data, toAlert)
  },

  async handleObsolete(detectionId: number, status: 'handled' | 'ignored', handlerId: number) {
    if (isMockEnabled()) {
      return inventoryMock.handleObsolete(detectionId, status, handlerId)
    }
    const response = await inventoryApi.handleObsoleteMaterialDetection({
      obsoleteMaterialHandleRequest: {
        detection_id: detectionId,
        handler_id: handlerId,
        status,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<ObsoleteMaterialDetection | undefined>)
    return mapOptional(data, toObsolete)
  },

  async listAlerts(query: InventoryAlertQuery) {
    const normalizedQuery = cleanQuery({
      ...query,
      endTime: toIsoDayBoundary(query.endTime, true),
      startTime: toIsoDayBoundary(query.startTime, false),
    })
    if (isMockEnabled()) {
      return inventoryMock.listAlerts(normalizedQuery)
    }
    const response = await inventoryApi.listInventoryAlert({
      endTime: normalizedQuery.endTime,
      materialId: normalizedQuery.materialId,
      page: normalizedQuery.page,
      pageSize: normalizedQuery.pageSize,
      startTime: normalizedQuery.startTime,
      status: normalizedQuery.status,
    })
    const payload = unwrap(response.data as ApiEnvelope<unknown>)
    return mapPageResult<InventoryAlertEvent, InventoryAlertItem>(payload, normalizedQuery, toAlert)
  },

  async listCompletionInbound(query: CompletionInboundQuery) {
    const normalizedQuery = cleanQuery({
      ...query,
      endTime: toIsoDayBoundary(query.endTime, true),
      startTime: toIsoDayBoundary(query.startTime, false),
    })
    if (isMockEnabled()) {
      return inventoryMock.listCompletionInbound(normalizedQuery)
    }
    const response = await inventoryApi.listCompletionInbound({
      inboundTimeEnd: normalizedQuery.endTime,
      inboundTimeStart: normalizedQuery.startTime,
      materialId: normalizedQuery.materialId,
      orderId: normalizedQuery.orderId,
      page: normalizedQuery.page,
      pageSize: normalizedQuery.pageSize,
    })
    const payload = unwrap(response.data as ApiEnvelope<unknown>)
    return mapPageResult<CompletionInboundOrder, CompletionInboundItem>(
      payload,
      normalizedQuery,
      toInbound,
    )
  },

  async listLocks(query: StockLockQuery) {
    const normalizedQuery = cleanQuery(query)
    if (isMockEnabled()) {
      return inventoryMock.listLocks(normalizedQuery)
    }
    const response = await inventoryApi.listMaterialStockLock({
      materialId: normalizedQuery.materialId,
      orderId: normalizedQuery.orderId,
      page: normalizedQuery.page,
      pageSize: normalizedQuery.pageSize,
      status: normalizedQuery.status,
    })
    const payload = unwrap(response.data as ApiEnvelope<unknown>)
    return mapPageResult<StockLockRecord, StockLockItem>(payload, normalizedQuery, toLock)
  },

  async listObsolete(query: ObsoleteMaterialQuery) {
    const normalizedQuery = cleanQuery({
      ...query,
      endTime: toIsoDayBoundary(query.endTime, true),
      startTime: toIsoDayBoundary(query.startTime, false),
    })
    if (isMockEnabled()) {
      return inventoryMock.listObsolete(normalizedQuery)
    }
    const response = await inventoryApi.listObsoleteMaterialDetection({
      detectTimeEnd: normalizedQuery.endTime,
      detectTimeStart: normalizedQuery.startTime,
      materialId: normalizedQuery.materialId,
      page: normalizedQuery.page,
      pageSize: normalizedQuery.pageSize,
      status: normalizedQuery.status,
    })
    const payload = unwrap(response.data as ApiEnvelope<unknown>)
    return mapPageResult<ObsoleteMaterialDetection, ObsoleteMaterialItem>(
      payload,
      normalizedQuery,
      toObsolete,
    )
  },

  async lockStock(form: StockLockFormData): Promise<StockLockResult> {
    if (isMockEnabled()) {
      return inventoryMock.lockStock(form)
    }
    const response = await inventoryApi.lockMaterialStock({
      materialStockLockRequest: {
        items: form.items.map((item) => ({
          lock_qty: item.lockQty,
          material_id: item.materialId,
        })),
        operator_id: form.operatorId,
        order_id: form.orderId,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<LockPayload | undefined>)
    return {
      items: (data?.records ?? []).map(toLock),
      shortages: (data?.shortages ?? []).map((item) => ({
        availableQty: item.available_qty,
        materialId: item.material_id,
        requiredQty: item.required_qty,
        shortageQty: item.shortage_qty,
      })),
      success: data?.success ?? false,
    }
  },

  async releaseLock(lockId: number, operatorId: number) {
    if (isMockEnabled()) {
      return inventoryMock.releaseLock(lockId, operatorId)
    }
    const response = await inventoryApi.releaseMaterialStock({
      materialStockReleaseRequest: { lock_id: lockId, operator_id: operatorId },
    })
    const data = unwrap(response.data as ApiEnvelope<StockLockRecord | undefined>)
    return mapOptional(data, toLock)
  },
}
