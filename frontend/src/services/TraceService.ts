import type {
  AffectedProductBatch,
  BatchConsumption,
  ConsumedMaterialBatch,
  MaterialBatchTraceResult,
  ProductBatchTraceResult,
  QualityImpactAnalyzeResult,
} from '@/api'
import type { ProductionOrderStatus } from '@/services/ProductionService'
import { qualityTraceabilityApi } from '@/api/client'

/** 质量影响分析建议处理动作。 */
export type SuggestedActionValue = 'freeze' | 'observe' | 'recall'

export interface PageResult<TEntity> {
  items: TEntity[]
  page: number
  pageSize: number
  total: number
}

export interface BatchConsumptionQuery {
  itemId?: number
  materialId?: number
  orderId?: number
  page: number
  pageSize: number
}

export interface BatchConsumptionItem {
  consumeQty: number
  consumptionId: number
  itemId: number
  materialName?: string
  orderId: number
  productMaterialName?: string
  productionStatus?: ProductionOrderStatus
}

export interface BatchConsumptionCreateFormData {
  consumeQty: number
  itemId: number
  orderId: number
}

export interface BatchConsumptionUpdateFormData {
  consumeQty: number
  consumptionId: number
  itemId: number
  orderId: number
}

export interface MaterialBatchTraceQuery {
  itemId?: number
  materialId?: number
  receiveDateEnd?: string
  receiveDateStart?: string
}

export interface AffectedProductItem {
  batchNo?: string
  consumeQty: number
  orderId: number
  productMaterialId: number
  productMaterialName?: string
  productionStatus?: ProductionOrderStatus
}

export interface MaterialBatchTraceItem {
  affectedProducts: AffectedProductItem[]
  itemId: number
  materialId: number
  materialName?: string
  supplierId?: number
  supplierName?: string
}

export interface ProductBatchTraceQuery {
  batchNo?: string
  includeSupplier?: boolean
  orderId?: number
}

export interface ConsumedMaterialItem {
  consumeQty: number
  itemId: number
  materialId: number
  materialName?: string
  orderId?: number
  receiveDate?: string
  supplierId?: number
  supplierName?: string
}

export interface ProductBatchTraceItem {
  batchNo?: string
  consumedBatches: ConsumedMaterialItem[]
  materialId: number
  materialName?: string
  orderId: number
}

export interface QualityImpactAnalyzeFormData {
  itemIds?: number[]
  materialId?: number
  receiveDateEnd?: string
  receiveDateStart?: string
}

export interface QualityImpactResult {
  affectedBatchCount?: number
  affectedOrderCount?: number
  affectedProducts: AffectedProductItem[]
  suggestedAction?: SuggestedActionValue
}

interface ApiEnvelope<TPayload> {
  code?: number
  data?: TPayload
  message?: string
}

interface RawPageResult {
  items?: unknown
  list?: unknown
  page?: unknown
  page_size?: unknown
  pageSize?: unknown
  records?: unknown
  rows?: unknown
  total?: unknown
}

class ApiRequestError extends Error {
  readonly status: number | undefined

  constructor(message: string, status?: number) {
    super(message)
    this.status = status
  }
}

function unwrap<TPayload>(payload: ApiEnvelope<TPayload>) {
  if (payload.code !== undefined && payload.code !== 200) {
    throw new ApiRequestError(payload.message || '接口请求失败', payload.code)
  }
  return payload.data
}

function getPageItems<TItem>(value: unknown): TItem[] {
  if (Array.isArray(value)) {
    return value as TItem[]
  }
  if (!value || typeof value !== 'object') {
    return []
  }
  const data = value as RawPageResult
  const items = data.records ?? data.items ?? data.list ?? data.rows
  if (Array.isArray(items)) {
    return items as TItem[]
  }
  return []
}

function getPageMetadata(
  value: unknown,
  fallback: Pick<PageResult<unknown>, 'page' | 'pageSize' | 'total'>,
) {
  if (!value || typeof value !== 'object') {
    return fallback
  }
  const data = value as RawPageResult
  let { page } = fallback
  let { pageSize } = fallback
  let { total } = fallback
  if (typeof data.page === 'number') {
    ;({ page } = data)
  }
  if (typeof data.page_size === 'number') {
    pageSize = data.page_size
  } else if (typeof data.pageSize === 'number') {
    ;({ pageSize } = data)
  }
  if (typeof data.total === 'number') {
    ;({ total } = data)
  }
  return { page, pageSize, total }
}

function optionalText(value: unknown) {
  if (typeof value === 'string' && value.trim()) {
    return value
  }
  return undefined
}

function optionalNumber(value: unknown) {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value
  }
  return undefined
}

function toBatchConsumption(record: BatchConsumption): BatchConsumptionItem {
  return {
    consumeQty: record.consume_qty,
    consumptionId: record.consumption_id,
    itemId: record.item_id,
    materialName: optionalText(record.purchase_item?.material_name),
    orderId: record.order_id,
    productMaterialName: optionalText(record.production_order?.material_name),
    productionStatus: record.production_order?.status,
  }
}

function toAffectedProduct(product: AffectedProductBatch): AffectedProductItem {
  return {
    batchNo: optionalText(product.batch_no),
    consumeQty: product.consume_qty,
    orderId: product.order_id,
    productMaterialId: product.product_material_id,
    productMaterialName: optionalText(product.product_material_name),
    productionStatus: product.production_status,
  }
}

function toMaterialBatchTrace(result: MaterialBatchTraceResult): MaterialBatchTraceItem {
  return {
    affectedProducts: (result.affected_products ?? []).map(toAffectedProduct),
    itemId: result.item_id,
    materialId: result.material_id,
    materialName: optionalText(result.material_name),
    supplierId: optionalNumber(result.supplier_id),
    supplierName: optionalText(result.supplier_name),
  }
}

function toConsumedMaterial(batch: ConsumedMaterialBatch): ConsumedMaterialItem {
  return {
    consumeQty: batch.consume_qty,
    itemId: batch.item_id,
    materialId: batch.material_id,
    materialName: optionalText(batch.material_name),
    orderId: optionalNumber(batch.order_id),
    receiveDate: optionalText(batch.receive_date),
    supplierId: optionalNumber(batch.supplier_id),
    supplierName: optionalText(batch.supplier_name),
  }
}

function toProductBatchTrace(result: ProductBatchTraceResult): ProductBatchTraceItem {
  return {
    batchNo: optionalText(result.batch_no),
    consumedBatches: (result.consumed_batches ?? []).map(toConsumedMaterial),
    materialId: result.material_id,
    materialName: optionalText(result.material_name),
    orderId: result.order_id,
  }
}

function toQualityImpact(result: QualityImpactAnalyzeResult): QualityImpactResult {
  return {
    affectedBatchCount: optionalNumber(result.affected_batch_count),
    affectedOrderCount: optionalNumber(result.affected_order_count),
    affectedProducts: (result.affected_products ?? []).map(toAffectedProduct),
    suggestedAction: result.suggested_action,
  }
}

/** 质量追溯接口的唯一入口；页面只允许通过此对象访问后端。 */
export const traceService = {
  async analyzeQualityImpact(form: QualityImpactAnalyzeFormData) {
    let itemIds: number[] | undefined = undefined
    if (form.itemIds?.length) {
      ;({ itemIds } = form)
    }
    const response = await qualityTraceabilityApi.analyzeQualityImpact({
      qualityImpactAnalyzeRequest: {
        item_ids: itemIds,
        material_id: form.materialId,
        receive_date_end: form.receiveDateEnd,
        receive_date_start: form.receiveDateStart,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<QualityImpactAnalyzeResult | undefined>)
    if (data) {
      return toQualityImpact(data)
    }
    return undefined
  },

  api: qualityTraceabilityApi,

  async createBatchConsumption(form: BatchConsumptionCreateFormData) {
    const response = await qualityTraceabilityApi.addBatchConsumption({
      batchConsumptionCreateRequest: {
        consume_qty: form.consumeQty,
        item_id: form.itemId,
        order_id: form.orderId,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<BatchConsumption | undefined>)
    if (data) {
      return toBatchConsumption(data)
    }
    return undefined
  },

  async deleteBatchConsumption(consumptionId: number) {
    const response = await qualityTraceabilityApi.deleteBatchConsumption({
      batchConsumptionDeleteRequest: { consumption_id: consumptionId },
    })
    unwrap(response.data as ApiEnvelope<unknown>)
  },

  async listBatchConsumption(
    query: BatchConsumptionQuery,
  ): Promise<PageResult<BatchConsumptionItem>> {
    const response = await qualityTraceabilityApi.listBatchConsumption({
      itemId: query.itemId,
      materialId: query.materialId,
      orderId: query.orderId,
      page: query.page,
      pageSize: query.pageSize,
    })
    const data = unwrap(response.data as ApiEnvelope<unknown>)
    const items = getPageItems<BatchConsumption>(data).map(toBatchConsumption)
    const metadata = getPageMetadata(data, {
      page: query.page,
      pageSize: query.pageSize,
      total: items.length,
    })
    return { items, ...metadata }
  },

  async traceMaterialBatch(query: MaterialBatchTraceQuery): Promise<MaterialBatchTraceItem[]> {
    const response = await qualityTraceabilityApi.traceMaterialBatch({
      itemId: query.itemId,
      materialId: query.materialId,
      receiveDateEnd: query.receiveDateEnd,
      receiveDateStart: query.receiveDateStart,
    })
    const data = unwrap(response.data as ApiEnvelope<MaterialBatchTraceResult[] | undefined>)
    return (data ?? []).map(toMaterialBatchTrace)
  },

  async traceProductBatch(query: ProductBatchTraceQuery) {
    const response = await qualityTraceabilityApi.traceProductBatch({
      batchNo: query.batchNo,
      includeSupplier: query.includeSupplier,
      orderId: query.orderId,
    })
    const data = unwrap(response.data as ApiEnvelope<ProductBatchTraceResult | undefined>)
    if (data) {
      return toProductBatchTrace(data)
    }
    return undefined
  },

  async updateBatchConsumption(form: BatchConsumptionUpdateFormData) {
    const response = await qualityTraceabilityApi.updateBatchConsumption({
      batchConsumptionUpdateRequest: {
        consume_qty: form.consumeQty,
        consumption_id: form.consumptionId,
        item_id: form.itemId,
        order_id: form.orderId,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<BatchConsumption | undefined>)
    if (data) {
      return toBatchConsumption(data)
    }
    return undefined
  },
}
