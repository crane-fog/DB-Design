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
  MaterialStock,
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
import { useAuthStore } from '@/stores/auth'

export type { PageResult }

export type InventoryStockData = Omit<
  InventoryStockItem,
  'materialType' | 'safetyStock' | 'status'
> &
  Partial<Pick<InventoryStockItem, 'materialType' | 'safetyStock' | 'status'>>

function canReadMaterialReferences() {
  return useAuthStore(pinia).roles.some((role) =>
    ['系统管理员', '生产管理员', '采购员'].includes(role),
  )
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

function toStockStatus(availableQty: number, lockedQty: number, safetyStock: number) {
  if (availableQty <= 0) {
    return 'zero' as const
  }
  if (availableQty < safetyStock) {
    return 'low' as const
  }
  if (lockedQty > 0) {
    return 'locked' as const
  }
  return 'normal' as const
}

function toStock(material: Material, stock?: MaterialStock): InventoryStockItem | undefined {
  if (material.material_id === undefined || !material.material_name || !material.material_type) {
    return undefined
  }
  const availableQty = stock?.available_qty ?? 0
  const lockedQty = stock?.locked_qty ?? 0
  const safetyStock = material.safety_stock ?? 0
  return {
    availableQty,
    lastInDate: optionalText(stock?.last_in_date),
    lastOutDate: optionalText(stock?.last_out_date),
    lockedQty,
    materialId: material.material_id,
    materialName: material.material_name,
    materialType: material.material_type,
    safetyStock,
    status: toStockStatus(availableQty, lockedQty, safetyStock),
    unit: material.unit,
  }
}

function paginateItems<TItem>(items: TItem[], page: number, pageSize: number): PageResult<TItem> {
  const safePage = Math.max(1, page)
  const safePageSize = Math.max(1, pageSize)
  const start = (safePage - 1) * safePageSize
  return {
    items: items.slice(start, start + safePageSize),
    page: safePage,
    pageSize: safePageSize,
    total: items.length,
  }
}

async function findPagedRecord<TItem>(
  loadPage: (page: number, pageSize: number) => Promise<PageResult<TItem>>,
  matches: (item: TItem) => boolean,
  notFoundMessage: string,
) {
  const pageSize = 100
  const firstPage = await loadPage(1, pageSize)
  const firstMatch = firstPage.items.find(matches)
  if (firstMatch) {
    return firstMatch
  }
  const pageCount = Math.ceil(firstPage.total / pageSize)
  const pages = await Promise.all(
    Array.from({ length: Math.max(0, pageCount - 1) }, (unusedValue, index) => {
      void unusedValue
      return loadPage(index + 2, pageSize)
    }),
  )
  for (const result of pages) {
    const match = result.items.find(matches)
    if (match) {
      return match
    }
  }
  throw new Error(notFoundMessage)
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

let stockCatalogPromise: Promise<InventoryStockItem[]> | undefined = undefined

function invalidateStockCatalog() {
  stockCatalogPromise = undefined
}

async function loadStockCatalog() {
  if (!stockCatalogPromise) {
    stockCatalogPromise = (async () => {
      const materials = await loadAllPageItems<Material>((page, pageSize) =>
        materialBomApi.listMaterialData({ page, pageSize }),
      )
      const stockResults = await Promise.all(
        materials.map(async (material) => {
          if (material.material_id === undefined) {
            return undefined
          }
          const response = await materialBomApi.getMaterialStockData({
            materialId: material.material_id,
          })
          const stock = requireData(response.data as ApiEnvelope<MaterialStock | undefined>)
          return toStock(material, stock)
        }),
      )
      return stockResults.filter((item): item is InventoryStockItem => item !== undefined)
    })().catch((error: unknown) => {
      stockCatalogPromise = undefined
      throw error
    })
  }
  return stockCatalogPromise
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
    invalidateStockCatalog()
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
    const alert = await findPagedRecord(
      (page, pageSize) => this.listAlerts({ page, pageSize }),
      (item) => item.alertId === alertId,
      '库存预警记录不存在或已删除',
    )
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
    const item = await findPagedRecord(
      (page, pageSize) => this.listCompletionInbound({ page, pageSize }),
      (record) => record.inboundId === inboundId,
      '完工入库记录不存在或已删除',
    )
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
    const item = await findPagedRecord(
      (page, pageSize) => this.listObsolete({ page, pageSize }),
      (record) => record.detectionId === detectionId,
      '呆滞物料检测记录不存在或已删除',
    )
    const stock = await this.getStockDetail(item.materialId)
    return {
      ...item,
      stock,
    }
  },

  async getOverview(): Promise<Partial<InventoryOverviewSummary>> {
    let stocksPromise: Promise<PageResult<InventoryStockData>> | undefined = undefined
    if (canReadMaterialReferences()) {
      stocksPromise = this.listStocks({ page: 1, pageSize: 100 })
    }
    const [alerts, locks, obsolete, inbound, firstStockPage] = await Promise.all([
      this.listAlerts({ page: 1, pageSize: 1, status: 'pending' }),
      this.listLocks({ page: 1, pageSize: 1, status: 'locked' }),
      this.listObsolete({ page: 1, pageSize: 1, status: 'pending' }),
      this.listCompletionInbound({ page: 1, pageSize: 1 }),
      stocksPromise,
    ])
    const summary = {
      inboundCount: inbound.total,
      lockedCount: locks.total,
      obsoletePendingCount: obsolete.total,
      pendingAlertCount: alerts.total,
    }
    if (!firstStockPage) {
      return summary
    }
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
    let materialItemsPromise = Promise.resolve<Material[]>([])
    let versionItemsPromise = Promise.resolve<BomVersion[]>([])
    if (canReadMaterialReferences()) {
      materialItemsPromise = loadAllPageItems<Material>((page, pageSize) =>
        materialBomApi.listMaterialData({ page, pageSize }),
      )
      versionItemsPromise = loadAllPageItems<BomVersion>((page, pageSize) =>
        materialBomApi.listBomVersionData({ effectiveOnly: true, page, pageSize }),
      )
    }
    let orderItemsPromise: Promise<ProductionOrderDetail[]> = Promise.resolve([])
    if (
      includeProductionOrders &&
      useAuthStore(pinia).roles.some((role) => ['系统管理员', '生产管理员'].includes(role))
    ) {
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
    const response = await materialBomApi.getMaterialStockData({ materialId })
    const stock = requireData(response.data as ApiEnvelope<MaterialStock>)
    if (canReadMaterialReferences()) {
      const materialResponse = await materialBomApi.getMaterialData({ materialId })
      const material = requireData(materialResponse.data as ApiEnvelope<Material>)
      const item = toStock(material, stock)
      if (!item) {
        throw new Error('物料数据无效')
      }
      return item
    }
    return {
      availableQty: stock.available_qty ?? 0,
      lastInDate: optionalText(stock.last_in_date),
      lastOutDate: optionalText(stock.last_out_date),
      lockedQty: stock.locked_qty ?? 0,
      materialId,
      materialName: `物料 #${materialId}`,
    }
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
      endTime: toIsoDayBoundary(query.endTime, true),
      startTime: toIsoDayBoundary(query.startTime, false),
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
      endTime: toIsoDayBoundary(query.endTime, true),
      startTime: toIsoDayBoundary(query.startTime, false),
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
      endTime: toIsoDayBoundary(query.endTime, true),
      startTime: toIsoDayBoundary(query.startTime, false),
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
    if (!canReadMaterialReferences()) {
      const items: InventoryStockData[] = []
      if (query.materialId) {
        items.push(await this.getStockDetail(query.materialId))
      }
      return paginateItems(items, query.page, query.pageSize)
    }
    const items = await loadStockCatalog()
    const materialName = query.materialName?.trim().toLowerCase()
    const filteredItems = items.filter(
      (item) =>
        (!query.materialId || item.materialId === query.materialId) &&
        (!materialName || item.materialName.toLowerCase().includes(materialName)) &&
        (!query.materialType || item.materialType === query.materialType) &&
        (!query.status || item.status === query.status),
    )
    return paginateItems(filteredItems, query.page, query.pageSize)
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
    if (result.success) {
      invalidateStockCatalog()
    }
    return result
  },

  refreshStockCatalog() {
    invalidateStockCatalog()
  },

  async releaseLock(lockId: number, operatorId: number) {
    const response = await inventoryApi.releaseMaterialStock({
      materialStockReleaseRequest: { lock_id: lockId, operator_id: operatorId },
    })
    const data = requireData(response.data as ApiEnvelope<StockLockRecord>)
    invalidateStockCatalog()
    return toLock(data)
  },
}
