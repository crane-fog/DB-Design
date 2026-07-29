import type {
  AffectedProductItem,
  BatchConsumptionCreateFormData,
  BatchConsumptionItem,
  BatchConsumptionQuery,
  BatchConsumptionUpdateFormData,
  MaterialBatchTraceItem,
  MaterialBatchTraceQuery,
  ProductBatchTraceItem,
  ProductBatchTraceQuery,
  QualityImpactAnalyzeFormData,
  QualityImpactResult,
} from '@/services/TraceService'
import type { PageResult } from '@/services/pagination'

const materialByItemId: Record<number, number> = {
  1101: 1001,
  1102: 1002,
  1103: 1003,
  1104: 1004,
}

const consumptionRecords: BatchConsumptionItem[] = [
  {
    consumeQty: 120,
    consumptionId: 7001,
    itemId: 1101,
    materialName: '铝合金型材 6061',
    orderId: 5001,
    productMaterialName: '智能控制终端 AX100',
    productionStatus: 'pending_review',
  },
  {
    consumeQty: 60,
    consumptionId: 7002,
    itemId: 1102,
    materialName: '控制板组件 C01',
    orderId: 5001,
    productMaterialName: '智能控制终端 AX100',
    productionStatus: 'pending_review',
  },
  {
    consumeQty: 200,
    consumptionId: 7003,
    itemId: 1103,
    materialName: '屏蔽线缆 0.25mm',
    orderId: 5002,
    productMaterialName: '模块化执行器 MX200',
    productionStatus: 'pending_schedule',
  },
  {
    consumeQty: 600,
    consumptionId: 7004,
    itemId: 1104,
    materialName: '内六角螺钉 M4',
    orderId: 5002,
    productMaterialName: '模块化执行器 MX200',
    productionStatus: 'pending_schedule',
  },
  {
    consumeQty: 100,
    consumptionId: 7005,
    itemId: 1101,
    materialName: '铝合金型材 6061',
    orderId: 5003,
    productMaterialName: '智能控制终端 AX100',
    productionStatus: 'in_progress',
  },
  {
    consumeQty: 50,
    consumptionId: 7006,
    itemId: 1102,
    materialName: '控制板组件 C01',
    orderId: 5004,
    productMaterialName: '模块化执行器 MX200',
    productionStatus: 'in_progress',
  },
]

const materialTraceRecords: MaterialBatchTraceItem[] = [
  {
    affectedProducts: [
      {
        batchNo: 'AX100-20260729-A',
        consumeQty: 120,
        orderId: 5001,
        productMaterialId: 2001,
        productMaterialName: '智能控制终端 AX100',
        productionStatus: 'pending_review',
      },
      {
        batchNo: 'AX100-20260730-B',
        consumeQty: 100,
        orderId: 5003,
        productMaterialId: 2001,
        productMaterialName: '智能控制终端 AX100',
        productionStatus: 'in_progress',
      },
    ],
    itemId: 1101,
    materialId: 1001,
    materialName: '铝合金型材 6061',
    supplierId: 41,
    supplierName: '华东精材供应链',
  },
  {
    affectedProducts: [
      {
        batchNo: 'AX100-20260729-A',
        consumeQty: 60,
        orderId: 5001,
        productMaterialId: 2001,
        productMaterialName: '智能控制终端 AX100',
        productionStatus: 'pending_review',
      },
      {
        batchNo: 'MX200-20260730-A',
        consumeQty: 50,
        orderId: 5004,
        productMaterialId: 2002,
        productMaterialName: '模块化执行器 MX200',
        productionStatus: 'in_progress',
      },
    ],
    itemId: 1102,
    materialId: 1002,
    materialName: '控制板组件 C01',
    supplierId: 42,
    supplierName: '启航电子组件',
  },
  {
    affectedProducts: [
      {
        batchNo: 'MX200-20260729-A',
        consumeQty: 200,
        orderId: 5002,
        productMaterialId: 2002,
        productMaterialName: '模块化执行器 MX200',
        productionStatus: 'pending_schedule',
      },
    ],
    itemId: 1103,
    materialId: 1003,
    materialName: '屏蔽线缆 0.25mm',
    supplierId: 41,
    supplierName: '华东精材供应链',
  },
  {
    affectedProducts: [
      {
        batchNo: 'MX200-20260729-A',
        consumeQty: 600,
        orderId: 5002,
        productMaterialId: 2002,
        productMaterialName: '模块化执行器 MX200',
        productionStatus: 'pending_schedule',
      },
    ],
    itemId: 1104,
    materialId: 1004,
    materialName: '内六角螺钉 M4',
    supplierId: 42,
    supplierName: '启航电子组件',
  },
]

const productTraceRecords: ProductBatchTraceItem[] = [
  {
    batchNo: 'AX100-20260729-A',
    consumedBatches: [
      {
        consumeQty: 120,
        itemId: 1101,
        materialId: 1001,
        materialName: '铝合金型材 6061',
        orderId: 5001,
        receiveDate: '2026-07-24',
        supplierId: 41,
        supplierName: '华东精材供应链',
      },
      {
        consumeQty: 60,
        itemId: 1102,
        materialId: 1002,
        materialName: '控制板组件 C01',
        orderId: 5001,
        receiveDate: '2026-07-26',
        supplierId: 42,
        supplierName: '启航电子组件',
      },
    ],
    materialId: 2001,
    materialName: '智能控制终端 AX100',
    orderId: 5001,
  },
  {
    batchNo: 'MX200-20260729-A',
    consumedBatches: [
      {
        consumeQty: 200,
        itemId: 1103,
        materialId: 1003,
        materialName: '屏蔽线缆 0.25mm',
        orderId: 5002,
        receiveDate: '2026-07-26',
        supplierId: 41,
        supplierName: '华东精材供应链',
      },
      {
        consumeQty: 600,
        itemId: 1104,
        materialId: 1004,
        materialName: '内六角螺钉 M4',
        orderId: 5002,
        receiveDate: '2026-07-27',
        supplierId: 42,
        supplierName: '启航电子组件',
      },
    ],
    materialId: 2002,
    materialName: '模块化执行器 MX200',
    orderId: 5002,
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
  materialTraceRecords: MaterialBatchTraceItem[]
  productTraceRecords: ProductBatchTraceItem[]
}

export function snapshotTraceMock(): TraceMockState {
  return structuredClone({ consumptionRecords, materialTraceRecords, productTraceRecords })
}

export function restoreTraceMock(state: TraceMockState) {
  consumptionRecords.splice(
    0,
    consumptionRecords.length,
    ...structuredClone(state.consumptionRecords),
  )
  materialTraceRecords.splice(
    0,
    materialTraceRecords.length,
    ...structuredClone(state.materialTraceRecords),
  )
  productTraceRecords.splice(
    0,
    productTraceRecords.length,
    ...structuredClone(state.productTraceRecords),
  )
}

function paginate<TItem>(items: TItem[], page: number, pageSize: number): PageResult<TItem> {
  const safePage = Math.max(1, page)
  const safePageSize = Math.max(1, pageSize)
  const start = (safePage - 1) * safePageSize
  return {
    items: structuredClone(items.slice(start, start + safePageSize)),
    page: safePage,
    pageSize: safePageSize,
    total: items.length,
  }
}

function listBatchConsumption(query: BatchConsumptionQuery) {
  return delay(() =>
    paginate(
      consumptionRecords.filter(
        (record) =>
          (!query.itemId || record.itemId === query.itemId) &&
          (!query.materialId ||
            record.itemId === query.itemId ||
            materialByItemId[record.itemId] === query.materialId) &&
          (!query.orderId || record.orderId === query.orderId),
      ),
      query.page,
      query.pageSize,
    ),
  )
}

function requirePositive(value: number, label: string) {
  if (!Number.isFinite(value) || value <= 0) {
    throw new Error(`${label}必须是大于 0 的有效数值`)
  }
}

function createBatchConsumption(form: BatchConsumptionCreateFormData) {
  return delay(() => {
    requirePositive(form.consumeQty, '领料数量')
    if (!materialByItemId[form.itemId]) {
      throw new Error('采购明细不存在')
    }
    if (!consumptionRecords.some((record) => record.orderId === form.orderId)) {
      throw new Error('生产订单不存在')
    }
    const source = consumptionRecords.find((record) => record.itemId === form.itemId)
    const record: BatchConsumptionItem = {
      consumeQty: form.consumeQty,
      consumptionId: Math.max(...consumptionRecords.map((item) => item.consumptionId), 7000) + 1,
      itemId: form.itemId,
      materialName: source?.materialName,
      orderId: form.orderId,
      productMaterialName: source?.productMaterialName,
      productionStatus: source?.productionStatus,
    }
    consumptionRecords.unshift(record)
    return structuredClone(record)
  })
}

function updateBatchConsumption(form: BatchConsumptionUpdateFormData) {
  return delay(() => {
    requirePositive(form.consumeQty, '领料数量')
    const record = consumptionRecords.find((item) => item.consumptionId === form.consumptionId)
    if (!record) {
      throw new Error('未找到领料记录')
    }
    if (!materialByItemId[form.itemId]) {
      throw new Error('采购明细不存在')
    }
    if (!consumptionRecords.some((item) => item.orderId === form.orderId)) {
      throw new Error('生产订单不存在')
    }
    Object.assign(record, {
      consumeQty: form.consumeQty,
      itemId: form.itemId,
      orderId: form.orderId,
    })
    return structuredClone(record)
  })
}

function deleteBatchConsumption(consumptionId: number) {
  return delay(() => {
    const index = consumptionRecords.findIndex((item) => item.consumptionId === consumptionId)
    if (index === -1) {
      throw new Error('未找到领料记录')
    }
    consumptionRecords.splice(index, 1)
  })
}

function traceMaterialBatch(query: MaterialBatchTraceQuery) {
  return delay(() =>
    materialTraceRecords
      .filter(
        (record) =>
          (!query.itemId || record.itemId === query.itemId) &&
          (!query.materialId || record.materialId === query.materialId),
      )
      .map((record) => structuredClone(record)),
  )
}

function traceProductBatch(query: ProductBatchTraceQuery) {
  return delay(() => {
    const result = productTraceRecords.find(
      (item) =>
        (!query.orderId || item.orderId === query.orderId) &&
        (!query.batchNo || item.batchNo === query.batchNo),
    )
    if (!result) {
      return undefined
    }
    const copy = structuredClone(result)
    if (!query.includeSupplier) {
      copy.consumedBatches.forEach((batch) => {
        batch.supplierId = undefined
        batch.supplierName = undefined
      })
    }
    return copy
  })
}

function analyzeQualityImpact(form: QualityImpactAnalyzeFormData): Promise<QualityImpactResult> {
  return delay(() => {
    const itemIds = new Set<number>()
    for (const itemId of form.itemIds ?? []) {
      itemIds.add(itemId)
    }
    const affectedProducts: AffectedProductItem[] = []
    for (const record of materialTraceRecords) {
      const matchesItem = !itemIds.size || itemIds.has(record.itemId)
      const matchesMaterial = !form.materialId || record.materialId === form.materialId
      if (matchesItem && matchesMaterial) {
        affectedProducts.push(...record.affectedProducts)
      }
    }
    let suggestedAction: QualityImpactResult['suggestedAction'] = 'observe'
    if (affectedProducts.length > 2) {
      suggestedAction = 'freeze'
    }
    return {
      affectedBatchCount: new Set(affectedProducts.map((item) => item.batchNo).filter(Boolean))
        .size,
      affectedOrderCount: new Set(affectedProducts.map((item) => item.orderId)).size,
      affectedProducts: structuredClone(affectedProducts),
      suggestedAction,
    }
  })
}

export const traceMock = {
  analyzeQualityImpact,
  createBatchConsumption,
  deleteBatchConsumption,
  listBatchConsumption,
  traceMaterialBatch,
  traceProductBatch,
  updateBatchConsumption,
}
