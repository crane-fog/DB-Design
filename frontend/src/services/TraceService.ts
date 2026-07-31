import type {
  AffectedProductBatch,
  BatchConsumption,
  ConsumedMaterialBatch,
  MaterialBatchTraceResult,
  ProductBatchTraceResult,
  QualityImpactAnalyzeResult,
} from '@/api'
import {
  type ApiEnvelope,
  type PageRequest,
  type PageResult,
  getPageItems,
  getPageMetadata,
  optionalNumber,
  optionalText,
  unwrap,
} from '@/services/pagination'
import type { ProductionOrderStatus } from '@/services/ProductionService'
import { isMockEnabled } from '@/config/mock'
import { qualityTraceabilityApi } from '@/api/client'
import { traceRepository as traceMock } from '@/mock/repositories'
import { useAuthStore } from '@/stores/auth'

export type { PageResult }

/** 质量影响分析建议处理动作。 */
export type SuggestedActionValue = 'freeze' | 'observe' | 'recall'

export interface BatchConsumptionQuery extends PageRequest {
  itemId?: number
  materialId?: number
  orderId?: number
}

export interface BatchConsumptionItem {
  consumedAt?: string
  consumeQty: number
  consumptionId: number
  itemId: number
  materialName?: string
  orderId: number
  operatorName?: string
  productBatchNo?: string
  productMaterialName?: string
  productionStatus?: ProductionOrderStatus
  purchaseOrderNo?: string
  remarks?: string
  supplierId?: number
  supplierName?: string
  unit?: string
  materialBatchNo?: string
}

export interface BatchConsumptionCreateFormData {
  consumedAt?: string
  consumeQty: number
  itemId: number
  orderId: number
  operatorName?: string
  productBatchNo?: string
  purchaseOrderNo?: string
  remarks?: string
  supplierId?: number
  unit?: string
  materialBatchNo?: string
}

export interface BatchConsumptionUpdateFormData {
  consumeQty: number
  consumptionId: number
  itemId: number
  orderId: number
  consumedAt?: string
  operatorName?: string
  productBatchNo?: string
  purchaseOrderNo?: string
  remarks?: string
  supplierId?: number
  unit?: string
  materialBatchNo?: string
}

export interface MaterialBatchTraceQuery {
  itemId?: number
  materialId?: number
  receiveDateEnd?: string
  receiveDateStart?: string
  supplierId?: number
}

export interface AffectedProductItem {
  batchNo?: string
  consumeQty: number
  defectiveQty?: number
  inboundQty?: number
  inboundAt?: string
  orderId: number
  planQty?: number
  producedAt?: string
  productMaterialId: number
  productMaterialName?: string
  productionStatus?: ProductionOrderStatus
  qualifiedQty?: number
  qualityStatus?: QualityDispositionStatus
  finishedQty?: number
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
  materialBatchNo?: string
  purchaseOrderNo?: string
  unit?: string
}

export interface ProductBatchTraceItem {
  batchNo?: string
  bomVersion?: string
  consumedBatches: ConsumedMaterialItem[]
  defectiveQty?: number
  finishedQty?: number
  inboundQty?: number
  inboundAt?: string
  materialId: number
  materialName?: string
  orderId: number
  planQty?: number
  producedAt?: string
  qualifiedQty?: number
  qualityStatus?: QualityDispositionStatus
}

export interface QualityImpactAnalyzeFormData {
  itemIds?: number[]
  materialId?: number
  receiveDateEnd?: string
  receiveDateStart?: string
}

export type QualityDispositionStatus = 'frozen' | 'pending' | 'recalled' | 'released'
export type QualityDispositionType = 'freeze' | 'recall'

export interface QualityDisposition {
  affectedQty?: number
  batchNo: string
  dispositionId: number
  handlingInstruction?: string
  materialName?: string
  note?: string
  operatedAt: string
  operatorName: string
  orderId: number
  reason: string
  recallScope?: string
  status: QualityDispositionStatus
  type: QualityDispositionType
}

export interface QualityDispositionFormData {
  affectedQty?: number
  batchNo: string
  handlingInstruction?: string
  note?: string
  operatorName: string
  reason: string
  recallScope?: string
}

export interface TraceSupplierOption {
  supplierId: number
  supplierName: string
}

export interface TraceConsumptionReferenceData {
  productBatches: {
    batchNo: string
    materialName: string
    orderId: number
  }[]
  purchaseItems: {
    itemId: number
    materialBatchNo: string
    materialName: string
    purchaseOrderNo: string
    supplierId: number
    supplierName: string
    unit: string
  }[]
}

export interface QualityImpactSummary {
  affectedBatchCount: number
  affectedOrderCount: number
  affectedProductCount: number
  defectiveQty: number
  frozenBatchCount: number
  inboundQty: number
  pendingBatchCount: number
  qualifiedQty: number
  recalledBatchCount: number
}

export interface QualityImpactResult {
  affectedBatchCount?: number
  affectedOrderCount?: number
  affectedProducts: AffectedProductItem[]
  suggestedAction?: SuggestedActionValue
  summary?: QualityImpactSummary
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

function assertMockPermission() {
  if (isMockEnabled() && !useAuthStore().hasPermission('trace:manage')) {
    throw new Error('当前账号没有执行追溯管理操作的权限')
  }
}

function assertConsumptionId(consumptionId: number) {
  if (!Number.isInteger(consumptionId) || consumptionId <= 0) {
    throw new Error('批次消耗记录 ID 无效')
  }
}

/** 质量追溯接口的唯一入口；页面只允许通过此对象访问后端。 */
export const traceService = {
  async analyzeQualityImpact(form: QualityImpactAnalyzeFormData) {
    if (isMockEnabled()) {
      return traceMock.analyzeQualityImpact(form)
    }
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
    assertMockPermission()
    if (isMockEnabled()) {
      return traceMock.createBatchConsumption(form)
    }
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
    assertMockPermission()
    assertConsumptionId(consumptionId)
    if (isMockEnabled()) {
      return traceMock.deleteBatchConsumption(consumptionId)
    }
    const response = await qualityTraceabilityApi.deleteBatchConsumption({
      batchConsumptionDeleteRequest: { consumption_id: consumptionId },
    })
    unwrap(response.data as ApiEnvelope<unknown>)
  },

  async freezeBatch(form: QualityDispositionFormData) {
    assertMockPermission()
    if (isMockEnabled()) {
      return traceMock.freezeBatch(form)
    }
    throw new Error('当前后端暂未提供质量冻结接口')
  },

  async getBatchConsumption(consumptionId: number): Promise<BatchConsumptionItem | undefined> {
    assertConsumptionId(consumptionId)
    if (isMockEnabled()) {
      return traceMock.getBatchConsumption(consumptionId)
    }
    throw new Error('当前后端暂未提供按 ID 查询批次消耗记录的接口')
  },

  async listBatchConsumption(
    query: BatchConsumptionQuery,
  ): Promise<PageResult<BatchConsumptionItem>> {
    if (isMockEnabled()) {
      return traceMock.listBatchConsumption(query)
    }
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

  async listConsumptionReferences() {
    if (isMockEnabled()) {
      return traceMock.listConsumptionReferences()
    }
    return { productBatches: [], purchaseItems: [] } satisfies TraceConsumptionReferenceData
  },

  async listQualityDispositions(batchNo?: string) {
    if (isMockEnabled()) {
      return traceMock.listQualityDispositions(batchNo)
    }
    return [] as QualityDisposition[]
  },

  async listTraceSuppliers() {
    if (isMockEnabled()) {
      return traceMock.listTraceSuppliers()
    }
    return [] as TraceSupplierOption[]
  },

  async recallBatch(form: QualityDispositionFormData) {
    assertMockPermission()
    if (isMockEnabled()) {
      return traceMock.recallBatch(form)
    }
    throw new Error('当前后端暂未提供质量召回接口')
  },

  async traceMaterialBatch(query: MaterialBatchTraceQuery): Promise<MaterialBatchTraceItem[]> {
    if (isMockEnabled()) {
      return traceMock.traceMaterialBatch(query)
    }
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
    if (isMockEnabled()) {
      return traceMock.traceProductBatch(query)
    }
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
    assertMockPermission()
    if (isMockEnabled()) {
      return traceMock.updateBatchConsumption(form)
    }
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
