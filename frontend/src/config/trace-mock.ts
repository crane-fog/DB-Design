import type {
  AffectedProductItem,
  BatchConsumptionCreateFormData,
  BatchConsumptionItem,
  BatchConsumptionQuery,
  BatchConsumptionUpdateFormData,
  ConsumedMaterialItem,
  MaterialBatchTraceItem,
  MaterialBatchTraceQuery,
  ProductBatchTraceItem,
  ProductBatchTraceQuery,
  QualityDisposition,
  QualityDispositionFormData,
  QualityDispositionStatus,
  QualityImpactAnalyzeFormData,
  QualityImpactResult,
  TraceConsumptionReferenceData,
  TraceSupplierOption,
} from '@/services/TraceService'
import type { PageResult } from '@/services/pagination'
import type { ProductionOrderStatus } from '@/services/ProductionService'

interface PurchaseItemReference {
  itemId: number
  materialBatchNo: string
  materialId: number
  materialName: string
  purchaseOrderNo: string
  receiveDate: string
  supplierId: number
  supplierName: string
  unit: string
}

interface ProductBatchReference {
  batchNo: string
  bomVersion: string
  defectiveQty: number
  finishedQty: number
  inboundQty: number
  inboundAt: string
  materialId: number
  materialName: string
  orderId: number
  planQty: number
  producedAt: string
  productionStatus: ProductionOrderStatus
  qualifiedQty: number
  qualityStatus: QualityDispositionStatus
}

const purchaseItems: PurchaseItemReference[] = [
  {
    itemId: 1101,
    materialBatchNo: 'AL6061-20260724-A',
    materialId: 1001,
    materialName: '铝合金型材 6061',
    purchaseOrderNo: 'PO-20260718-001',
    receiveDate: '2026-07-24',
    supplierId: 41,
    supplierName: '华东精材供应链',
    unit: 'kg',
  },
  {
    itemId: 1102,
    materialBatchNo: 'C01-20260726-B',
    materialId: 1002,
    materialName: '控制板组件 C01',
    purchaseOrderNo: 'PO-20260719-003',
    receiveDate: '2026-07-26',
    supplierId: 42,
    supplierName: '启航电子组件',
    unit: '件',
  },
  {
    itemId: 1103,
    materialBatchNo: 'CABLE-20260726-A',
    materialId: 1003,
    materialName: '屏蔽线缆 0.25mm',
    purchaseOrderNo: 'PO-20260720-006',
    receiveDate: '2026-07-26',
    supplierId: 41,
    supplierName: '华东精材供应链',
    unit: 'm',
  },
  {
    itemId: 1104,
    materialBatchNo: 'M4-20260727-C',
    materialId: 1004,
    materialName: '内六角螺钉 M4',
    purchaseOrderNo: 'PO-20260721-008',
    receiveDate: '2026-07-27',
    supplierId: 42,
    supplierName: '启航电子组件',
    unit: '颗',
  },
]

const productBatches: ProductBatchReference[] = [
  {
    batchNo: 'AX100-20260724-C',
    bomVersion: 'BOM-AX100-V3.2',
    defectiveQty: 1,
    finishedQty: 80,
    inboundAt: '2026-07-24 16:30:00',
    inboundQty: 79,
    materialId: 2001,
    materialName: '智能控制终端 AX100',
    orderId: 5005,
    planQty: 80,
    producedAt: '2026-07-24 14:10:00',
    productionStatus: 'completed',
    qualifiedQty: 79,
    qualityStatus: 'recalled',
  },
  {
    batchNo: 'MX200-20260721-C',
    bomVersion: 'BOM-MX200-V2.1',
    defectiveQty: 0,
    finishedQty: 120,
    inboundAt: '2026-07-21 17:10:00',
    inboundQty: 120,
    materialId: 2002,
    materialName: '模块化执行器 MX200',
    orderId: 5010,
    planQty: 120,
    producedAt: '2026-07-21 15:40:00',
    productionStatus: 'completed',
    qualifiedQty: 120,
    qualityStatus: 'frozen',
  },
  {
    batchNo: 'AX100-20260729-A',
    bomVersion: 'BOM-AX100-V3.2',
    defectiveQty: 2,
    finishedQty: 100,
    inboundAt: '2026-07-29 18:00:00',
    inboundQty: 98,
    materialId: 2001,
    materialName: '智能控制终端 AX100',
    orderId: 5001,
    planQty: 100,
    producedAt: '2026-07-29 16:20:00',
    productionStatus: 'pending_review',
    qualifiedQty: 96,
    qualityStatus: 'pending',
  },
  {
    batchNo: 'MX200-20260729-A',
    bomVersion: 'BOM-MX200-V2.1',
    defectiveQty: 3,
    finishedQty: 160,
    inboundAt: '2026-07-29 17:40:00',
    inboundQty: 157,
    materialId: 2002,
    materialName: '模块化执行器 MX200',
    orderId: 5002,
    planQty: 160,
    producedAt: '2026-07-29 16:10:00',
    productionStatus: 'pending_schedule',
    qualifiedQty: 154,
    qualityStatus: 'pending',
  },
  {
    batchNo: 'AX100-20260730-B',
    bomVersion: 'BOM-AX100-V3.2',
    defectiveQty: 0,
    finishedQty: 60,
    inboundAt: '2026-07-30 11:20:00',
    inboundQty: 60,
    materialId: 2001,
    materialName: '智能控制终端 AX100',
    orderId: 5003,
    planQty: 60,
    producedAt: '2026-07-30 10:45:00',
    productionStatus: 'in_progress',
    qualifiedQty: 60,
    qualityStatus: 'pending',
  },
  {
    batchNo: 'MX200-20260730-A',
    bomVersion: 'BOM-MX200-V2.1',
    defectiveQty: 0,
    finishedQty: 50,
    inboundAt: '2026-07-30 13:00:00',
    inboundQty: 50,
    materialId: 2002,
    materialName: '模块化执行器 MX200',
    orderId: 5004,
    planQty: 50,
    producedAt: '2026-07-30 12:25:00',
    productionStatus: 'in_progress',
    qualifiedQty: 50,
    qualityStatus: 'pending',
  },
]

const consumptionRecords: BatchConsumptionItem[] = [
  { consumeQty: 120, consumptionId: 7001, itemId: 1101, orderId: 5001 },
  { consumeQty: 60, consumptionId: 7002, itemId: 1102, orderId: 5001 },
  { consumeQty: 200, consumptionId: 7003, itemId: 1103, orderId: 5002 },
  { consumeQty: 600, consumptionId: 7004, itemId: 1104, orderId: 5002 },
  { consumeQty: 100, consumptionId: 7005, itemId: 1101, orderId: 5003 },
  { consumeQty: 50, consumptionId: 7006, itemId: 1102, orderId: 5004 },
  { consumeQty: 60, consumptionId: 7007, itemId: 1101, orderId: 5005 },
  { consumeQty: 30, consumptionId: 7008, itemId: 1102, orderId: 5005 },
  { consumeQty: 120, consumptionId: 7009, itemId: 1103, orderId: 5010 },
  { consumeQty: 360, consumptionId: 7010, itemId: 1104, orderId: 5010 },
]

const qualityDispositions: QualityDisposition[] = [
  {
    batchNo: 'MX200-20260721-C',
    dispositionId: 9001,
    materialName: '模块化执行器 MX200',
    note: '待完成来料复检后决定是否解除。',
    operatedAt: '2026-07-28 09:30:00',
    operatorName: '质量主管',
    orderId: 5010,
    reason: '供应商来料抽检异常，需隔离在库成品。',
    status: 'frozen',
    type: 'freeze',
  },
  {
    batchNo: 'AX100-20260724-C',
    dispositionId: 9002,
    materialName: '智能控制终端 AX100',
    note: '仅为追溯模块内的 Mock 召回记录。',
    operatedAt: '2026-07-29 14:20:00',
    operatorName: '质量主管',
    orderId: 5005,
    reason: '现场反馈控制板批次存在潜在失效风险。',
    status: 'recalled',
    type: 'recall',
  },
]

function delay<TResult>(factory: () => TResult): Promise<TResult> {
  return new Promise((resolve, reject) => {
    globalThis.setTimeout(() => {
      try {
        resolve(factory())
      } catch (error) {
        reject(error)
      }
    }, 120)
  })
}

export interface TraceMockState {
  consumptionRecords: BatchConsumptionItem[]
  productBatches: ProductBatchReference[]
  qualityDispositions: QualityDisposition[]
}

export function snapshotTraceMock(): TraceMockState {
  return structuredClone({ consumptionRecords, productBatches, qualityDispositions })
}

export function restoreTraceMock(state: TraceMockState) {
  consumptionRecords.splice(
    0,
    consumptionRecords.length,
    ...structuredClone(state.consumptionRecords),
  )
  if (Array.isArray(state.productBatches)) {
    productBatches.splice(0, productBatches.length, ...structuredClone(state.productBatches))
  }
  if (Array.isArray(state.qualityDispositions)) {
    qualityDispositions.splice(
      0,
      qualityDispositions.length,
      ...structuredClone(state.qualityDispositions),
    )
  }
}

function requirePositive(value: number, label: string) {
  if (!Number.isFinite(value) || value <= 0) {
    throw new Error(`${label}必须是大于 0 的有效数值`)
  }
}

function findPurchaseItem(itemId: number) {
  const item = purchaseItems.find((candidate) => candidate.itemId === itemId)
  if (!item) {
    throw new Error('采购明细不存在')
  }
  return item
}

function findProductBatch(orderId: number, batchNo?: string) {
  const batch = productBatches.find(
    (candidate) =>
      (!orderId || candidate.orderId === orderId) && (!batchNo || candidate.batchNo === batchNo),
  )
  if (!batch) {
    throw new Error('生产订单或成品批次不存在')
  }
  return batch
}

function requireDateTime(value: string | undefined) {
  if (!value || Number.isNaN(Date.parse(value.replace(' ', 'T')))) {
    throw new Error('消耗时间格式无效')
  }
}

function requireConsumptionReference(
  form: BatchConsumptionCreateFormData | BatchConsumptionUpdateFormData,
) {
  if (!form.productBatchNo?.trim()) {
    throw new Error('请选择产品批次')
  }
  const purchaseItem = findPurchaseItem(form.itemId)
  const productBatch = findProductBatch(form.orderId, form.productBatchNo)
  if (productBatch.batchNo !== form.productBatchNo) {
    throw new Error('生产订单与产品批次不匹配')
  }
  if (form.supplierId && form.supplierId !== purchaseItem.supplierId) {
    throw new Error('供应商与采购明细不匹配')
  }
  return { productBatch, purchaseItem }
}

function toConsumptionRecord(
  record: BatchConsumptionItem,
  form: BatchConsumptionCreateFormData | BatchConsumptionUpdateFormData = record,
): BatchConsumptionItem {
  const purchaseItem = findPurchaseItem(record.itemId)
  const productBatch = findProductBatch(record.orderId, form.productBatchNo)
  return {
    consumeQty: record.consumeQty,
    consumedAt: form.consumedAt ?? record.consumedAt ?? '2026-07-30 10:00:00',
    consumptionId: record.consumptionId,
    itemId: purchaseItem.itemId,
    materialBatchNo: purchaseItem.materialBatchNo,
    materialName: purchaseItem.materialName,
    operatorName: form.operatorName ?? record.operatorName ?? '追溯专员',
    orderId: productBatch.orderId,
    productBatchNo: productBatch.batchNo,
    productMaterialName: productBatch.materialName,
    productionStatus: productBatch.productionStatus,
    purchaseOrderNo: purchaseItem.purchaseOrderNo,
    remarks: form.remarks ?? record.remarks,
    supplierId: purchaseItem.supplierId,
    supplierName: purchaseItem.supplierName,
    unit: purchaseItem.unit,
  }
}

function hydrateConsumptionRecords() {
  consumptionRecords.forEach((record, index) => {
    consumptionRecords[index] = toConsumptionRecord(record)
  })
}

function paginate<TItem>(items: TItem[], page: number, pageSize: number): PageResult<TItem> {
  const total = items.length
  const safePageSize = Math.max(1, pageSize)
  const safePage = Math.min(Math.max(1, page), Math.max(1, Math.ceil(total / safePageSize)))
  const start = (safePage - 1) * safePageSize
  return {
    items: structuredClone(items.slice(start, start + safePageSize)),
    page: safePage,
    pageSize: safePageSize,
    total,
  }
}

function listBatchConsumption(query: BatchConsumptionQuery) {
  return delay(() => {
    hydrateConsumptionRecords()
    return paginate(
      consumptionRecords.filter((record) => {
        const purchaseItem = findPurchaseItem(record.itemId)
        return (
          (!query.itemId || record.itemId === query.itemId) &&
          (!query.materialId || purchaseItem.materialId === query.materialId) &&
          (!query.orderId || record.orderId === query.orderId)
        )
      }),
      query.page,
      query.pageSize,
    )
  })
}

function createBatchConsumption(form: BatchConsumptionCreateFormData) {
  return delay(() => {
    requirePositive(form.consumeQty, '消耗数量')
    requireDateTime(form.consumedAt)
    const { purchaseItem, productBatch } = requireConsumptionReference(form)
    const record = toConsumptionRecord(
      {
        consumeQty: form.consumeQty,
        consumptionId: Math.max(...consumptionRecords.map((item) => item.consumptionId), 7000) + 1,
        itemId: purchaseItem.itemId,
        orderId: productBatch.orderId,
      },
      form,
    )
    consumptionRecords.unshift(record)
    return structuredClone(record)
  })
}

function updateBatchConsumption(form: BatchConsumptionUpdateFormData) {
  return delay(() => {
    requirePositive(form.consumeQty, '消耗数量')
    requireDateTime(form.consumedAt)
    const index = consumptionRecords.findIndex((item) => item.consumptionId === form.consumptionId)
    if (index === -1) {
      throw new Error('未找到批次消耗记录')
    }
    requireConsumptionReference(form)
    const updated = toConsumptionRecord(
      {
        consumeQty: form.consumeQty,
        consumptionId: form.consumptionId,
        itemId: form.itemId,
        orderId: form.orderId,
      },
      form,
    )
    consumptionRecords.splice(index, 1, updated)
    return structuredClone(updated)
  })
}

function deleteBatchConsumption(consumptionId: number) {
  return delay(() => {
    const index = consumptionRecords.findIndex((item) => item.consumptionId === consumptionId)
    if (index === -1) {
      throw new Error('未找到批次消耗记录，可能已被删除')
    }
    consumptionRecords.splice(index, 1)
  })
}

function getBatchConsumption(consumptionId: number) {
  return delay(() => {
    hydrateConsumptionRecords()
    const record = consumptionRecords.find((item) => item.consumptionId === consumptionId)
    if (!record) {
      throw new Error('未找到批次消耗记录，可能已被删除')
    }
    return structuredClone(record)
  })
}

function toAffectedProduct(record: BatchConsumptionItem): AffectedProductItem {
  const batch = findProductBatch(record.orderId, record.productBatchNo)
  return {
    batchNo: batch.batchNo,
    consumeQty: record.consumeQty,
    defectiveQty: batch.defectiveQty,
    finishedQty: batch.finishedQty,
    inboundAt: batch.inboundAt,
    inboundQty: batch.inboundQty,
    orderId: batch.orderId,
    planQty: batch.planQty,
    producedAt: batch.producedAt,
    productMaterialId: batch.materialId,
    productMaterialName: batch.materialName,
    productionStatus: batch.productionStatus,
    qualifiedQty: batch.qualifiedQty,
    qualityStatus: batch.qualityStatus,
  }
}

function traceMaterialBatch(query: MaterialBatchTraceQuery) {
  return delay(() => {
    hydrateConsumptionRecords()
    const matched = consumptionRecords.filter((record) => {
      const purchaseItem = findPurchaseItem(record.itemId)
      return (
        (!query.itemId || record.itemId === query.itemId) &&
        (!query.materialId || purchaseItem.materialId === query.materialId) &&
        (!query.supplierId || purchaseItem.supplierId === query.supplierId) &&
        (!query.receiveDateStart || purchaseItem.receiveDate >= query.receiveDateStart) &&
        (!query.receiveDateEnd || purchaseItem.receiveDate <= query.receiveDateEnd)
      )
    })
    const grouped = new Map<number, BatchConsumptionItem[]>()
    matched.forEach((record) =>
      grouped.set(record.itemId, [...(grouped.get(record.itemId) ?? []), record]),
    )
    return [...grouped.entries()].map(([itemId, records]): MaterialBatchTraceItem => {
      const purchaseItem = findPurchaseItem(itemId)
      return {
        affectedProducts: records.map(toAffectedProduct),
        itemId,
        materialId: purchaseItem.materialId,
        materialName: purchaseItem.materialName,
        supplierId: purchaseItem.supplierId,
        supplierName: purchaseItem.supplierName,
      }
    })
  })
}

function traceProductBatch(query: ProductBatchTraceQuery) {
  return delay(() => {
    hydrateConsumptionRecords()
    const batch = productBatches.find(
      (candidate) =>
        (!query.orderId || candidate.orderId === query.orderId) &&
        (!query.batchNo || candidate.batchNo === query.batchNo),
    )
    if (!batch) {
      return undefined
    }
    const consumedBatches = consumptionRecords
      .filter(
        (record) => record.orderId === batch.orderId && record.productBatchNo === batch.batchNo,
      )
      .map((record) => {
        const purchaseItem = findPurchaseItem(record.itemId)
        const consumedBatch: ConsumedMaterialItem = {
          consumeQty: record.consumeQty,
          itemId: purchaseItem.itemId,
          materialBatchNo: purchaseItem.materialBatchNo,
          materialId: purchaseItem.materialId,
          materialName: purchaseItem.materialName,
          orderId: Number(purchaseItem.purchaseOrderNo.replace(/\D/g, '')) || undefined,
          purchaseOrderNo: purchaseItem.purchaseOrderNo,
          receiveDate: purchaseItem.receiveDate,
          supplierId: purchaseItem.supplierId,
          supplierName: purchaseItem.supplierName,
          unit: purchaseItem.unit,
        }
        if (query.includeSupplier === false) {
          delete consumedBatch.supplierId
          delete consumedBatch.supplierName
        }
        return consumedBatch
      })
    return structuredClone({ ...batch, consumedBatches }) satisfies ProductBatchTraceItem
  })
}

function getMatchedConsumptions(form: QualityImpactAnalyzeFormData) {
  const itemIds = new Set<number>()
  form.itemIds?.forEach((itemId) => itemIds.add(itemId))
  return consumptionRecords.filter((record) => {
    const purchaseItem = findPurchaseItem(record.itemId)
    return (
      (!itemIds.size || itemIds.has(record.itemId)) &&
      (!form.materialId || purchaseItem.materialId === form.materialId) &&
      (!form.receiveDateStart || purchaseItem.receiveDate >= form.receiveDateStart) &&
      (!form.receiveDateEnd || purchaseItem.receiveDate <= form.receiveDateEnd)
    )
  })
}

function analyzeQualityImpact(form: QualityImpactAnalyzeFormData): Promise<QualityImpactResult> {
  return delay(() => {
    hydrateConsumptionRecords()
    const affectedProducts = getMatchedConsumptions(form).map(toAffectedProduct)
    const uniqueProducts = [
      ...new Map(affectedProducts.map((item) => [item.batchNo, item])).values(),
    ]
    const summary = {
      affectedBatchCount: uniqueProducts.length,
      affectedOrderCount: new Set(uniqueProducts.map((item) => item.orderId)).size,
      affectedProductCount: uniqueProducts.reduce(
        (total, item) => total + (item.finishedQty ?? 0),
        0,
      ),
      defectiveQty: uniqueProducts.reduce((total, item) => total + (item.defectiveQty ?? 0), 0),
      frozenBatchCount: uniqueProducts.filter((item) => item.qualityStatus === 'frozen').length,
      inboundQty: uniqueProducts.reduce((total, item) => total + (item.inboundQty ?? 0), 0),
      pendingBatchCount: uniqueProducts.filter((item) => item.qualityStatus === 'pending').length,
      qualifiedQty: uniqueProducts.reduce((total, item) => total + (item.qualifiedQty ?? 0), 0),
      recalledBatchCount: uniqueProducts.filter((item) => item.qualityStatus === 'recalled').length,
    }
    let suggestedAction: QualityImpactResult['suggestedAction'] = 'observe'
    if (uniqueProducts.length > 1) {
      suggestedAction = 'freeze'
    }
    if (summary.recalledBatchCount) {
      suggestedAction = 'recall'
    }
    return {
      affectedBatchCount: summary.affectedBatchCount,
      affectedOrderCount: summary.affectedOrderCount,
      affectedProducts: structuredClone(uniqueProducts),
      suggestedAction,
      summary,
    }
  })
}

function requireDispositionTarget(batchNo: string) {
  if (!batchNo.trim()) {
    throw new Error('请选择需要处置的产品批次')
  }
  return findProductBatch(0, batchNo)
}

function createDisposition(type: QualityDisposition['type'], form: QualityDispositionFormData) {
  return delay(() => {
    if (!form.reason.trim()) {
      if (type === 'freeze') {
        throw new Error('冻结原因不能为空')
      }
      throw new Error('召回原因不能为空')
    }
    if (type === 'recall' && !form.recallScope?.trim()) {
      throw new Error('召回范围不能为空')
    }
    if (type === 'recall' && (!Number.isFinite(form.affectedQty) || (form.affectedQty ?? 0) <= 0)) {
      throw new Error('受影响数量必须是大于 0 的有效数值')
    }
    const batch = requireDispositionTarget(form.batchNo)
    if (type === 'freeze' && batch.qualityStatus !== 'pending') {
      throw new Error('该批次已处于质量处置状态，不能重复冻结')
    }
    if (type === 'recall' && batch.qualityStatus === 'recalled') {
      throw new Error('该批次已召回，不能重复召回')
    }
    let status: QualityDispositionStatus = 'recalled'
    if (type === 'freeze') {
      status = 'frozen'
    }
    const disposition: QualityDisposition = {
      batchNo: batch.batchNo,
      dispositionId: Math.max(...qualityDispositions.map((item) => item.dispositionId), 9000) + 1,
      materialName: batch.materialName,
      note: form.note?.trim() || undefined,
      operatedAt: new Date().toISOString().slice(0, 19).replace('T', ' '),
      operatorName: form.operatorName.trim() || '当前操作员',
      orderId: batch.orderId,
      reason: form.reason.trim(),
      status,
      type,
    }
    if (type === 'recall') {
      disposition.affectedQty = form.affectedQty
      disposition.handlingInstruction = form.handlingInstruction?.trim() || undefined
      disposition.recallScope = form.recallScope?.trim()
    }
    batch.qualityStatus = disposition.status
    qualityDispositions.unshift(disposition)
    return structuredClone(disposition)
  })
}

function freezeBatch(form: QualityDispositionFormData) {
  return createDisposition('freeze', form)
}

function recallBatch(form: QualityDispositionFormData) {
  return createDisposition('recall', form)
}

function listQualityDispositions(batchNo?: string) {
  return delay(() =>
    structuredClone(qualityDispositions.filter((record) => !batchNo || record.batchNo === batchNo)),
  )
}

function listTraceSuppliers() {
  return delay(
    () =>
      structuredClone([
        ...new Map(
          purchaseItems.map((item) => [
            item.supplierId,
            { supplierId: item.supplierId, supplierName: item.supplierName },
          ]),
        ).values(),
      ]) satisfies TraceSupplierOption[],
  )
}

function listConsumptionReferences() {
  return delay(
    () =>
      structuredClone({
        productBatches: productBatches.map(({ batchNo, materialName, orderId }) => ({
          batchNo,
          materialName,
          orderId,
        })),
        purchaseItems: purchaseItems.map(
          ({
            itemId,
            materialBatchNo,
            materialName,
            purchaseOrderNo,
            supplierId,
            supplierName,
            unit,
          }) => ({
            itemId,
            materialBatchNo,
            materialName,
            purchaseOrderNo,
            supplierId,
            supplierName,
            unit,
          }),
        ),
      }) satisfies TraceConsumptionReferenceData,
  )
}

export const traceMock = {
  analyzeQualityImpact,
  createBatchConsumption,
  deleteBatchConsumption,
  freezeBatch,
  getBatchConsumption,
  listBatchConsumption,
  listConsumptionReferences,
  listQualityDispositions,
  listTraceSuppliers,
  recallBatch,
  traceMaterialBatch,
  traceProductBatch,
  updateBatchConsumption,
}
