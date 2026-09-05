import type {
  AffectedProductBatch,
  BatchConsumption,
  CompletionInboundOrder,
  ConsumedMaterialBatch,
  MaterialBatchTraceResult,
  ProductionOrderBrief,
  ProductionOrderStatus,
  PurchaseOrder,
  SupplierDetail,
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
import { inventoryApi, productionApi, purchaseApi, qualityTraceabilityApi } from '@/api/client'
import { useAuthStore } from '@/stores/auth'

export type { PageResult }

export interface BatchConsumptionQuery extends PageRequest {
  itemId?: number
  materialId?: number
  orderId?: number
}

export interface BatchConsumptionItem {
  consumeQty: number
  consumptionId: number
  itemId: number
  materialId?: number
  materialName?: string
  orderId: number
  productMaterialName?: string
  productionStatus?: ProductionOrderStatus
  purchaseOrderId?: number
}

export interface BatchConsumptionCreateFormData {
  consumeQty: number
  itemId: number
  orderId: number
}

export interface BatchConsumptionUpdateFormData extends BatchConsumptionCreateFormData {
  consumptionId: number
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
  orderId?: number
}

export interface ConsumedMaterialItem {
  consumeQty: number
  itemId: number
  materialId: number
  materialName?: string
  purchaseOrderId?: number
  receiveDate?: string
  supplierId?: number
  supplierName?: string
}

export interface ProductBatchTraceItem {
  batchNo?: string
  bomVersion?: string
  consumedBatches: ConsumedMaterialItem[]
  finishedQty?: number
  materialId: number
  materialName?: string
  orderId: number
  planQty?: number
  producedAt?: string
  inboundRecords?: CompletionInboundOrder[]
}

export interface TraceSupplierOption {
  supplierId: number
  supplierName: string
}

export interface TraceConsumptionReferenceData {
  productionOrders?: { materialName?: string; orderId: number; status: ProductionOrderStatus }[]
  purchaseItems?: {
    itemId: number
    materialName?: string
    purchaseOrderId: number
    supplierName?: string
  }[]
  suppliers?: TraceSupplierOption[]
}

function toBatchConsumption(record: BatchConsumption): BatchConsumptionItem {
  return {
    consumeQty: record.consume_qty,
    consumptionId: record.consumption_id,
    itemId: record.item_id,
    materialId: optionalNumber(record.purchase_item?.material_id),
    materialName: optionalText(record.purchase_item?.material_name),
    orderId: record.order_id,
    productMaterialName: optionalText(record.production_order?.material_name),
    productionStatus: record.production_order?.status,
    purchaseOrderId: optionalNumber(record.purchase_item?.order_id),
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
    purchaseOrderId: optionalNumber(batch.order_id),
    receiveDate: optionalText(batch.receive_date),
    supplierId: optionalNumber(batch.supplier_id),
    supplierName: optionalText(batch.supplier_name),
  }
}

function requireData<TPayload>(payload: ApiEnvelope<TPayload>): TPayload {
  const data = unwrap(payload)
  if (data === undefined || data === null) {
    throw new Error('接口未返回有效数据')
  }
  return data
}

// 遵循 UserContextService 的跨模块读权限，避免辅助查询触发无权限跳转。
function canReadProduction() {
  const auth = useAuthStore()
  return auth.hasRole('系统管理员') || auth.hasRole('生产管理员')
}

function canReadPurchase() {
  const auth = useAuthStore()
  return ['系统管理员', '采购员', '采购主管'].some((role) => auth.hasRole(role))
}

function canReadInventory() {
  return canReadProduction() || useAuthStore().hasRole('库存管理员')
}

async function readAllPages<TRecord>(
  request: (page: number, pageSize: number) => Promise<{ data: ApiEnvelope<unknown> }>,
  page = 1,
  records: TRecord[] = [],
): Promise<TRecord[]> {
  const response = await request(page, 100)
  const data = requireData(response.data)
  const items = getPageItems<TRecord>(data)
  const metadata = getPageMetadata(data, { page, pageSize: 100, total: items.length })
  records.push(...items)
  if (records.length >= metadata.total) {
    return records
  }
  if (!items.length || metadata.page !== page) {
    throw new Error('分页数据不完整，请重新查询')
  }
  return readAllPages(request, page + 1, records)
}

function assertConsumptionId(consumptionId: number) {
  if (!Number.isSafeInteger(consumptionId) || consumptionId <= 0) {
    throw new Error('批次消耗记录 ID 无效')
  }
}

/** 质量追溯只通过生成的 API 访问真实后端。 */
export const traceService = {
  async createBatchConsumption(form: BatchConsumptionCreateFormData) {
    const response = await qualityTraceabilityApi.addBatchConsumption({
      batchConsumptionCreateRequest: {
        consume_qty: form.consumeQty,
        item_id: form.itemId,
        order_id: form.orderId,
      },
    })
    return toBatchConsumption(requireData(response.data))
  },

  async deleteBatchConsumption(consumptionId: number) {
    assertConsumptionId(consumptionId)
    const response = await qualityTraceabilityApi.deleteBatchConsumption({
      batchConsumptionDeleteRequest: { consumption_id: consumptionId },
    })
    unwrap(response.data)
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
    const data = requireData(response.data)
    const items = getPageItems<BatchConsumption>(data).map(toBatchConsumption)
    return { items, ...getPageMetadata(data, { ...query, total: items.length }) }
  },

  async listConsumptionReferences(): Promise<TraceConsumptionReferenceData> {
    const references: TraceConsumptionReferenceData = {}
    if (canReadProduction()) {
      const orders = await readAllPages<ProductionOrderBrief>((page, pageSize) =>
        productionApi.listProductionOrder({ page, pageSize }),
      )
      references.productionOrders = orders.map((order) => ({
        materialName: optionalText(order.material_name),
        orderId: order.order_id,
        status: order.status,
      }))
    }
    if (canReadPurchase()) {
      const [orders, suppliers] = await Promise.all([
        readAllPages<PurchaseOrder>((page, pageSize) =>
          purchaseApi.listPurchaseOrder({ page, pageSize }),
        ),
        readAllPages<SupplierDetail>((page, pageSize) =>
          purchaseApi.listSupplierData({ page, pageSize }),
        ),
      ])
      references.purchaseItems = orders.flatMap((order) =>
        (order.details ?? []).map((item) => ({
          itemId: item.item_id,
          materialName: optionalText(item.material_name),
          purchaseOrderId: order.order_id,
          supplierName: optionalText(order.supplier?.supplier_name),
        })),
      )
      references.suppliers = suppliers.map((supplier) => ({
        supplierId: supplier.supplier_id,
        supplierName: supplier.supplier_name,
      }))
    }
    return references
  },

  async traceMaterialBatch(query: MaterialBatchTraceQuery): Promise<MaterialBatchTraceItem[]> {
    const hasTraceFilter =
      query.itemId ||
      query.materialId ||
      query.supplierId ||
      (query.receiveDateStart && query.receiveDateEnd)
    if (!hasTraceFilter) {
      throw new Error('请提供采购明细、原材料、供应商或完整到货日期范围')
    }
    const response = await qualityTraceabilityApi.traceMaterialBatch({
      itemId: query.itemId,
      materialId: query.materialId,
      receiveDateEnd: query.receiveDateEnd,
      receiveDateStart: query.receiveDateStart,
      supplierId: query.supplierId,
    })
    return requireData(response.data).map(toMaterialBatchTrace)
  },

  async traceProductBatch(query: ProductBatchTraceQuery): Promise<ProductBatchTraceItem> {
    const response = await qualityTraceabilityApi.traceProductBatch({
      batchNo: query.batchNo,
      includeSupplier: true,
      orderId: query.orderId,
    })
    const data = requireData(response.data)
    const result: ProductBatchTraceItem = {
      batchNo: optionalText(data.batch_no),
      consumedBatches: (data.consumed_batches ?? []).map(toConsumedMaterial),
      materialId: data.material_id,
      materialName: optionalText(data.material_name),
      orderId: data.order_id,
    }
    if (canReadProduction()) {
      const orderResponse = await productionApi.getProductionOrder({ orderId: data.order_id })
      const order = requireData(orderResponse.data)
      result.bomVersion = optionalText(order.version_no)
      result.planQty = optionalNumber(order.plan_qty)
      result.finishedQty = optionalNumber(order.finished_qty)
      result.producedAt = optionalText(order.actual_end)
    }
    if (canReadInventory()) {
      const inbounds = await readAllPages<CompletionInboundOrder>((page, pageSize) =>
        inventoryApi.listCompletionInbound({ orderId: data.order_id, page, pageSize }),
      )
      result.inboundRecords = inbounds.filter(
        (item) => !result.batchNo || item.batch_no === result.batchNo,
      )
    }
    return result
  },

  async updateBatchConsumption(form: BatchConsumptionUpdateFormData) {
    assertConsumptionId(form.consumptionId)
    const response = await qualityTraceabilityApi.updateBatchConsumption({
      batchConsumptionUpdateRequest: {
        consume_qty: form.consumeQty,
        consumption_id: form.consumptionId,
        item_id: form.itemId,
        order_id: form.orderId,
      },
    })
    return toBatchConsumption(requireData(response.data))
  },
}
