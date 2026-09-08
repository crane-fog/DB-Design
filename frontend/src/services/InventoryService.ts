import {
  type ApiEnvelope,
  type PageResult,
  getPageItems,
  getPageMetadata,
  mapPageResult,
  optionalText,
  unwrap,
} from '@/services/pagination'
import type {
  MaterialShortageItem as ApiMaterialShortageItem,
  BomVersion,
  CompletionInboundOrder,
  InventoryAlertEvent,
  Material,
  MaterialShortageCalculateResponseAllOfData,
  MaterialStockDetail,
  MaterialStockLockData,
  ObsoleteMaterialDetection,
  ProductionOrderDetail,
  StockLockRecord,
} from '@/api'
import type {
  CompletionInboundDetail,
  CompletionInboundFormData,
  CompletionInboundItem,
  CompletionInboundQuery,
  InventoryAlertDetail,
  InventoryAlertGenerateResult,
  InventoryAlertItem,
  InventoryAlertQuery,
  InventoryOverviewSummary,
  InventoryReferenceData,
  InventoryStockItem,
  InventoryStockQuery,
  MaterialShortageRequestItem,
  MaterialShortageResult,
  ObsoleteDetectionResult,
  ObsoleteMaterialDetail,
  ObsoleteMaterialItem,
  ObsoleteMaterialQuery,
  StockLockFormData,
  StockLockItem,
  StockLockQuery,
  StockLockResult,
} from '@/types/inventory'
import { inventoryApi, materialBomApi, productionApi } from '@/api/client'
import { cleanQuery } from '@/services/request'
import { pinia } from '@/stores/pinia'
import { toUtcDayBoundary } from '@/utils/time'
import { useAuthStore } from '@/stores/auth'
import { PermissionCode } from '@/constants/permissions'

export type { PageResult }

export type InventoryStockData = Omit<
  InventoryStockItem,
  'materialType' | 'safetyStock' | 'status'
> &
  Partial<Pick<InventoryStockItem, 'materialType' | 'safetyStock' | 'status'>>

function canReadMaterialReferences() {
  return useAuthStore(pinia).hasPermission(PermissionCode.MaterialItemView)
}

interface AlertGeneratePayload {
  generated_count?: number
  records?: InventoryAlertEvent[]
  skipped_pending_count?: number
}

interface ObsoleteDetectPayload {
  detected_count?: number
  records?: ObsoleteMaterialDetection[]
}

function requireData<TData>(response: ApiEnvelope<TData>) {
  const data = unwrap(response)
  if (response.code !== 200 || data === undefined || data === null) {
    throw new Error('接口响应数据无效')
  }
  return data
}

function getLockData(envelope: ApiEnvelope<MaterialStockLockData>) {
  if (envelope.code === 409 && envelope.data?.success === false) {
    return envelope.data
  }
  return requireData(envelope)
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
    operatorName: optionalText(item.operator_name),
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

function toStock(item: MaterialStockDetail): InventoryStockItem {
  return {
    availableQty: item.available_qty,
    lastInDate: optionalText(item.last_in_date),
    lastOutDate: optionalText(item.last_out_date),
    lockedQty: item.locked_qty,
    materialId: item.material_id,
    materialName: item.material_name,
    materialType: item.material_type,
    safetyStock: item.safety_stock,
    status: item.status,
    unit: item.unit,
  }
}

async function loadAllPageItems<TItem>(
  loadPage: (page: number, pageSize: number) => Promise<{ data: unknown }>,
) {
  const pageSize = 100
  const firstResponse = await loadPage(1, pageSize)
  const firstPayload = requireData(firstResponse.data as ApiEnvelope<unknown>)
  const firstItems = getPageItems<TItem>(firstPayload)
  const metadata = getPageMetadata(firstPayload, { page: 1, pageSize, total: firstItems.length })
  const remainingPageCount = Math.ceil(metadata.total / metadata.pageSize) - 1
  if (remainingPageCount <= 0) {
    return firstItems
  }
  const remainingResponses = await Promise.all(
    Array.from({ length: remainingPageCount }, (unusedValue, index) => {
      void unusedValue
      return loadPage(index + 2, metadata.pageSize)
    }),
  )
  return [
    ...firstItems,
    ...remainingResponses.flatMap((response) => {
      const payload = requireData(response.data as ApiEnvelope<unknown>)
      return getPageItems<TItem>(payload)
    }),
  ]
}

export const inventoryService = {
  async addCompletionInbound(form: CompletionInboundFormData) {
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
    const data = requireData(response.data as ApiEnvelope<CompletionInboundOrder>)
    return toInbound(data)
  },

  async calculateShortage(items: MaterialShortageRequestItem[]): Promise<MaterialShortageResult> {
    const response = await inventoryApi.calculateMaterialShortage({
      materialShortageCalculateRequest: {
        items: items.map((item) => ({
          material_id: item.materialId,
          production_qty: item.productionQty,
          version_id: item.versionId,
        })),
      },
    })
    const data = requireData(
      response.data as ApiEnvelope<MaterialShortageCalculateResponseAllOfData>,
    )
    return {
      calculatedAt: data.calculation_time,
      items: data.records.map(toShortage),
    }
  },

  canReadMaterialReferences,

  async detectObsolete(
    idleDaysThreshold: number,
    materialId?: number,
  ): Promise<ObsoleteDetectionResult> {
    const response = await inventoryApi.detectObsoleteMaterial({
      obsoleteMaterialDetectRequest: {
        idle_days_threshold: idleDaysThreshold,
        material_id: materialId,
      },
    })
    const data = requireData(response.data as ApiEnvelope<ObsoleteDetectPayload>)
    const items = (data.records ?? []).map(toObsolete)
    return {
      detectedCount: data.detected_count ?? items.length,
      items,
    }
  },

  async generateAlerts(materialId?: number): Promise<InventoryAlertGenerateResult> {
    let request: { material_id: number } | undefined = undefined
    if (materialId !== undefined) {
      request = { material_id: materialId }
    }
    const response = await inventoryApi.generateInventoryAlert({
      inventoryAlertGenerateRequest: request,
    })
    const data = requireData(response.data as ApiEnvelope<AlertGeneratePayload>)
    return {
      generatedCount: data.generated_count ?? 0,
      items: (data.records ?? []).map(toAlert),
      skippedPendingCount: data.skipped_pending_count ?? 0,
    }
  },

  async getAlertDetail(
    alertId: number,
  ): Promise<Omit<InventoryAlertDetail, 'stock'> & { stock: InventoryStockData }> {
    const response = await inventoryApi.getInventoryAlert({ alertId })
    const alert = toAlert(requireData(response.data as ApiEnvelope<InventoryAlertEvent>))
    const stock = await this.getStockDetail(alert.materialId)
    let recommendedAction = '请复核可用库存与安全库存，并按实际情况补充或调整库存。'
    if (stock.status === 'zero') {
      recommendedAction = '当前库存为零，请优先补充库存并复核相关锁定。'
    }
    return {
      ...alert,
      recommendedAction,
      stock,
    }
  },

  async getCompletionInboundDetail(inboundId: number): Promise<CompletionInboundDetail> {
    const response = await inventoryApi.getCompletionInbound({ inboundId })
    const item = toInbound(requireData(response.data as ApiEnvelope<CompletionInboundOrder>))
    const references = await this.getReferenceData()
    const version = references.bomVersions.find((record) => record.versionId === item.versionId)
    const order = references.productionOrders.find((record) => record.orderId === item.orderId)
    const detail: CompletionInboundDetail = {
      ...item,
      bomVersionNo: version?.versionNo,
    }
    if (order) {
      detail.productionOrder = {
        materialName: order.materialName,
        orderId: order.orderId,
        status: order.status,
      }
    }
    return detail
  },

  async getObsoleteDetail(detectionId: number): Promise<
    Omit<ObsoleteMaterialDetail, 'activeOrders' | 'bomVersions' | 'stock'> & {
      stock: InventoryStockData
    }
  > {
    const response = await inventoryApi.getObsoleteMaterialDetection({ detectionId })
    const item = toObsolete(requireData(response.data as ApiEnvelope<ObsoleteMaterialDetection>))
    const stock = await this.getStockDetail(item.materialId)
    return {
      ...item,
      stock,
    }
  },

  async getOverview(): Promise<Partial<InventoryOverviewSummary>> {
    const auth = useAuthStore(pinia)
    const summary: Partial<InventoryOverviewSummary> = {}
    if (auth.hasPermission(PermissionCode.InventoryAlertView)) {
      const alerts = await this.listAlerts({ page: 1, pageSize: 1, status: 'pending' })
      summary.pendingAlertCount = alerts.total
    }
    if (auth.hasPermission(PermissionCode.InventoryLockView)) {
      const locks = await this.listLocks({ page: 1, pageSize: 1, status: 'locked' })
      summary.lockedCount = locks.total
    }
    if (auth.hasPermission(PermissionCode.InventoryObsoleteView)) {
      const obsolete = await this.listObsolete({ page: 1, pageSize: 1, status: 'pending' })
      summary.obsoletePendingCount = obsolete.total
    }
    if (auth.hasPermission(PermissionCode.InventoryCompletionView)) {
      const inbound = await this.listCompletionInbound({ page: 1, pageSize: 1 })
      summary.inboundCount = inbound.total
    }
    if (!auth.hasPermission(PermissionCode.InventoryStockView)) {
      return summary
    }
    const firstStockPage = await this.listStocks({ page: 1, pageSize: 100 })
    const remainingPageCount = Math.ceil(firstStockPage.total / firstStockPage.pageSize) - 1
    let stocks = [...firstStockPage.items]
    if (remainingPageCount > 0) {
      const remainingPages = await Promise.all(
        Array.from({ length: remainingPageCount }, (unusedValue, index) => {
          void unusedValue
          return this.listStocks({ page: index + 2, pageSize: firstStockPage.pageSize })
        }),
      )
      stocks = [...stocks, ...remainingPages.flatMap((page) => page.items)]
    }
    return {
      ...summary,
      availableMaterialCount: stocks.filter((item) => item.availableQty > 0).length,
      lockedMaterialCount: stocks.filter((item) => item.lockedQty > 0).length,
      lowStockCount: stocks.filter((item) => item.status === 'low').length,
      materialCount: firstStockPage.total,
      zeroStockCount: stocks.filter((item) => item.status === 'zero').length,
    }
  },

  async getReferenceData(includeProductionOrders = true): Promise<InventoryReferenceData> {
    const auth = useAuthStore(pinia)
    let materialItemsPromise = Promise.resolve<Material[]>([])
    let versionItemsPromise = Promise.resolve<BomVersion[]>([])
    if (auth.hasPermission(PermissionCode.MaterialItemView)) {
      materialItemsPromise = loadAllPageItems<Material>((page, pageSize) =>
        materialBomApi.listMaterialData({ page, pageSize }),
      )
    }
    if (auth.hasPermission(PermissionCode.MaterialBomVersionView)) {
      versionItemsPromise = loadAllPageItems<BomVersion>((page, pageSize) =>
        materialBomApi.listBomVersionData({ effectiveOnly: true, page, pageSize }),
      )
    }
    let orderItemsPromise: Promise<ProductionOrderDetail[]> = Promise.resolve([])
    if (includeProductionOrders && auth.hasPermission(PermissionCode.ProductionOrderView)) {
      orderItemsPromise = loadAllPageItems<ProductionOrderDetail>((page, pageSize) =>
        productionApi.listProductionOrder({ page, pageSize }),
      )
    }
    const [materialItems, versionItems, orderItems] = await Promise.all([
      materialItemsPromise,
      versionItemsPromise,
      orderItemsPromise,
    ])
    const materials = materialItems
      .filter(
        (
          item,
        ): item is Material & {
          material_id: number
          material_name: string
          material_type: NonNullable<Material['material_type']>
        } =>
          item.material_id !== undefined &&
          Boolean(item.material_name) &&
          item.material_type !== undefined,
      )
      .map((item) => ({
        materialId: item.material_id,
        materialName: item.material_name,
        materialType: item.material_type,
        unit: item.unit,
      }))
    const bomVersions = versionItems
      .filter(
        (
          item,
        ): item is BomVersion & {
          material_id: number
          version_id: number
          version_no: string
        } =>
          item.material_id !== undefined &&
          item.version_id !== undefined &&
          Boolean(item.version_no),
      )
      .map((item) => ({
        materialId: item.material_id,
        versionId: item.version_id,
        versionNo: item.version_no,
      }))
    const productionOrders = orderItems.map((order) => {
      const finishedQty = order.finished_qty ?? 0
      return {
        finishedQty,
        materialId: order.material_id,
        materialName: optionalText(order.material_name) ?? `物料 #${order.material_id}`,
        orderId: order.order_id,
        planQty: order.plan_qty,
        remainingQty: Math.max(0, order.plan_qty - finishedQty),
        status: order.status,
        versionId: order.version_id,
        versionNo: optionalText(order.version_no),
      }
    })
    return { bomVersions, materials, productionOrders }
  },

  async getStockDetail(materialId: number): Promise<InventoryStockData> {
    const response = await inventoryApi.listMaterialStockData({
      materialId,
      page: 1,
      pageSize: 1,
    })
    const payload = requireData(response.data as ApiEnvelope<unknown>)
    const [item] = getPageItems<MaterialStockDetail>(payload)
    if (!item) {
      throw new Error('物料库存数据不存在或已删除')
    }
    return toStock(item)
  },

  async handleAlert(alertId: number, status: 'handled' | 'ignored', handlerId: number) {
    const response = await inventoryApi.handleInventoryAlert({
      inventoryAlertHandleRequest: { alert_id: alertId, handler_id: handlerId, status },
    })
    const data = requireData(response.data as ApiEnvelope<InventoryAlertEvent>)
    return toAlert(data)
  },

  async handleObsolete(detectionId: number, status: 'handled' | 'ignored', handlerId: number) {
    const response = await inventoryApi.handleObsoleteMaterialDetection({
      obsoleteMaterialHandleRequest: {
        detection_id: detectionId,
        handler_id: handlerId,
        status,
      },
    })
    const data = requireData(response.data as ApiEnvelope<ObsoleteMaterialDetection>)
    return toObsolete(data)
  },

  async listAlerts(query: InventoryAlertQuery) {
    const normalizedQuery = cleanQuery({
      ...query,
      endTime: toUtcDayBoundary(query.endTime, true),
      startTime: toUtcDayBoundary(query.startTime, false),
    })
    const response = await inventoryApi.listInventoryAlert({
      endTime: normalizedQuery.endTime,
      materialId: normalizedQuery.materialId,
      page: normalizedQuery.page,
      pageSize: normalizedQuery.pageSize,
      startTime: normalizedQuery.startTime,
      status: normalizedQuery.status,
    })
    const payload = requireData(response.data as ApiEnvelope<unknown>)
    return mapPageResult<InventoryAlertEvent, InventoryAlertItem>(payload, normalizedQuery, toAlert)
  },

  async listCompletionInbound(query: CompletionInboundQuery) {
    const normalizedQuery = cleanQuery({
      ...query,
      endTime: toUtcDayBoundary(query.endTime, true),
      startTime: toUtcDayBoundary(query.startTime, false),
    })
    const response = await inventoryApi.listCompletionInbound({
      inboundTimeEnd: normalizedQuery.endTime,
      inboundTimeStart: normalizedQuery.startTime,
      materialId: normalizedQuery.materialId,
      orderId: normalizedQuery.orderId,
      page: normalizedQuery.page,
      pageSize: normalizedQuery.pageSize,
    })
    const payload = requireData(response.data as ApiEnvelope<unknown>)
    return mapPageResult<CompletionInboundOrder, CompletionInboundItem>(
      payload,
      normalizedQuery,
      toInbound,
    )
  },

  async listLocks(query: StockLockQuery) {
    const normalizedQuery = cleanQuery(query)
    const response = await inventoryApi.listMaterialStockLock({
      materialId: normalizedQuery.materialId,
      orderId: normalizedQuery.orderId,
      page: normalizedQuery.page,
      pageSize: normalizedQuery.pageSize,
      status: normalizedQuery.status,
    })
    const payload = requireData(response.data as ApiEnvelope<unknown>)
    return mapPageResult<StockLockRecord, StockLockItem>(payload, normalizedQuery, toLock)
  },

  async listObsolete(query: ObsoleteMaterialQuery) {
    const normalizedQuery = cleanQuery({
      ...query,
      endTime: toUtcDayBoundary(query.endTime, true),
      startTime: toUtcDayBoundary(query.startTime, false),
    })
    const response = await inventoryApi.listObsoleteMaterialDetection({
      detectTimeEnd: normalizedQuery.endTime,
      detectTimeStart: normalizedQuery.startTime,
      materialId: normalizedQuery.materialId,
      page: normalizedQuery.page,
      pageSize: normalizedQuery.pageSize,
      status: normalizedQuery.status,
    })
    const payload = requireData(response.data as ApiEnvelope<unknown>)
    return mapPageResult<ObsoleteMaterialDetection, ObsoleteMaterialItem>(
      payload,
      normalizedQuery,
      toObsolete,
    )
  },

  async listStocks(query: InventoryStockQuery): Promise<PageResult<InventoryStockData>> {
    const normalizedQuery = cleanQuery(query)
    const response = await inventoryApi.listMaterialStockData({
      materialId: normalizedQuery.materialId,
      materialName: normalizedQuery.materialName,
      materialType: normalizedQuery.materialType,
      page: normalizedQuery.page,
      pageSize: normalizedQuery.pageSize,
      status: normalizedQuery.status,
    })
    const payload = requireData(response.data as ApiEnvelope<unknown>)
    return mapPageResult<MaterialStockDetail, InventoryStockItem>(payload, normalizedQuery, toStock)
  },

  async lockStock(form: StockLockFormData): Promise<StockLockResult> {
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
    const envelope = response.data as ApiEnvelope<MaterialStockLockData>
    // 库存不足时后端返回 409 和缺口明细，交给页面展示失败原因。
    const data = getLockData(envelope)
    const result = {
      items: data.records.map(toLock),
      shortages: data.shortages.map((item) => ({
        availableQty: item.available_qty,
        materialId: item.material_id,
        requiredQty: item.required_qty,
        shortageQty: item.shortage_qty,
      })),
      success: data.success,
    }
    return result
  },

  async releaseLock(lockId: number, operatorId: number) {
    const response = await inventoryApi.releaseMaterialStock({
      materialStockReleaseRequest: { lock_id: lockId, operator_id: operatorId },
    })
    const data = requireData(response.data as ApiEnvelope<StockLockRecord>)
    return toLock(data)
  },
}
