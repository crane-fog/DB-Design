import type {
  AffectedProductItem,
  BatchConsumptionItem,
  BatchConsumptionQuery,
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
  listBatchConsumption,
  traceMaterialBatch,
  traceProductBatch,
}
