import type {
  CompletionInboundFormData,
  CompletionInboundItem,
  CompletionInboundQuery,
  InventoryAlertGenerateResult,
  InventoryAlertItem,
  InventoryAlertQuery,
  InventoryReferenceData,
  InventoryStockItem,
  InventoryStockQuery,
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
import type { PageResult } from '@/services/pagination'

const materialNames: Record<number, string> = {
  1001: '铝合金型材 6061',
  1002: '控制板组件 C01',
  1003: '屏蔽线缆 0.25mm',
  1004: '内六角螺钉 M4',
  2001: '智能控制终端 AX100',
  2002: '模块化执行器 MX200',
}

const materialMetadata: Record<
  number,
  {
    lastInDate?: string
    lastOutDate?: string
    materialType: InventoryStockItem['materialType']
    safetyStock: number
    unit: string
  }
> = {
  1001: {
    lastInDate: '2026-07-24',
    lastOutDate: '2026-07-27',
    materialType: 'raw_material',
    safetyStock: 40,
    unit: 'kg',
  },
  1002: {
    lastInDate: '2026-07-20',
    lastOutDate: '2026-07-26',
    materialType: 'semi_finished',
    safetyStock: 30,
    unit: '件',
  },
  1003: {
    lastInDate: '2026-04-18',
    lastOutDate: '2026-03-21',
    materialType: 'raw_material',
    safetyStock: 30,
    unit: 'm',
  },
  1004: {
    lastInDate: '2026-07-22',
    lastOutDate: '2026-07-23',
    materialType: 'auxiliary',
    safetyStock: 200,
    unit: '件',
  },
  2001: {
    lastInDate: '2026-07-27',
    lastOutDate: '2026-07-28',
    materialType: 'finished',
    safetyStock: 20,
    unit: '台',
  },
  2002: {
    lastInDate: '2026-07-26',
    lastOutDate: '2026-07-27',
    materialType: 'finished',
    safetyStock: 20,
    unit: '台',
  },
}

const stockByMaterial: Record<number, number> = {
  1001: 36,
  1002: 18,
  1003: 62,
  1004: 520,
  2001: 24,
  2002: 12,
}

interface MockBomComponent {
  children?: MockBomComponent[]
  lossRate: number
  materialId: number
  quantity: number
}

interface MockBomVersion {
  components: MockBomComponent[]
  materialId: number
  versionNo: string
}

const mockBomVersions: Record<number, MockBomVersion> = {
  22: {
    components: [
      { lossRate: 0.03, materialId: 1001, quantity: 1.8 },
      {
        children: [{ lossRate: 0.03, materialId: 1003, quantity: 0.8 }],
        lossRate: 0.02,
        materialId: 1002,
        quantity: 0.5,
      },
      { lossRate: 0.02, materialId: 1003, quantity: 2.5 },
      { lossRate: 0.01, materialId: 1004, quantity: 8 },
    ],
    materialId: 2002,
    versionNo: 'MX200-V2.2',
  },
  31: {
    components: [
      { lossRate: 0.05, materialId: 1001, quantity: 2.5 },
      {
        children: [{ lossRate: 0.04, materialId: 1003, quantity: 1.5 }],
        lossRate: 0.03,
        materialId: 1002,
        quantity: 1,
      },
      { lossRate: 0.02, materialId: 1004, quantity: 12 },
    ],
    materialId: 2001,
    versionNo: 'AX100-V3.1',
  },
  32: {
    components: [
      { lossRate: 0.04, materialId: 1001, quantity: 2.3 },
      {
        children: [{ lossRate: 0.03, materialId: 1003, quantity: 1.6 }],
        lossRate: 0.02,
        materialId: 1002,
        quantity: 1,
      },
      { lossRate: 0.01, materialId: 1003, quantity: 0.2 },
      { lossRate: 0.01, materialId: 1004, quantity: 12 },
    ],
    materialId: 2001,
    versionNo: 'AX100-V3.2',
  },
}

let alerts: InventoryAlertItem[] = [
  {
    alertId: 301,
    alertTime: '2026-07-27T08:32:00',
    alertType: 'low_stock',
    availableQty: 18,
    materialId: 1002,
    materialName: materialNames[1002],
    status: 'pending',
    threshold: 30,
  },
  {
    alertId: 302,
    alertTime: '2026-07-26T15:20:00',
    alertType: 'low_stock',
    availableQty: 12,
    materialId: 2002,
    materialName: materialNames[2002],
    status: 'pending',
    threshold: 20,
  },
  {
    alertId: 303,
    alertTime: '2026-07-25T10:05:00',
    alertType: 'low_stock',
    availableQty: 36,
    handleTime: '2026-07-25T13:10:00',
    handlerId: 1,
    materialId: 1001,
    materialName: materialNames[1001],
    status: 'handled',
    threshold: 40,
  },
]

let locks: StockLockItem[] = [
  {
    lockId: 501,
    lockQty: 18,
    lockTime: '2026-07-27T09:15:00',
    materialId: 1001,
    materialName: materialNames[1001],
    operatorId: 1,
    orderId: 7012,
    status: 'locked',
  },
  {
    lockId: 502,
    lockQty: 10,
    lockTime: '2026-07-26T11:40:00',
    materialId: 1002,
    materialName: materialNames[1002],
    operatorId: 1,
    orderId: 7011,
    status: 'locked',
  },
  {
    lockId: 503,
    lockQty: 120,
    lockTime: '2026-07-23T14:20:00',
    materialId: 1004,
    materialName: materialNames[1004],
    operatorId: 2,
    orderId: 7008,
    status: 'consumed',
  },
]

let obsoleteItems: ObsoleteMaterialItem[] = [
  {
    activeOrderIds: [],
    availableQty: 45,
    bomVersionIds: [],
    detectTime: '2026-07-27T07:50:00',
    detectionId: 801,
    idleDays: 128,
    lastOutDate: '2026-03-21',
    materialId: 1003,
    materialName: materialNames[1003],
    status: 'pending',
  },
  {
    activeOrderIds: [7012],
    availableQty: 80,
    bomVersionIds: [31, 32],
    detectTime: '2026-07-25T07:50:00',
    detectionId: 802,
    handlerId: 1,
    idleDays: 96,
    lastOutDate: '2026-04-20',
    materialId: 1004,
    materialName: materialNames[1004],
    status: 'ignored',
  },
]

let inbounds: CompletionInboundItem[] = [
  {
    batchNo: 'AX100-20260724-C',
    finishQty: 60,
    inboundId: 903,
    inboundTime: '2026-07-24T17:20:00',
    materialId: 2001,
    operatorId: 1,
    orderId: 5005,
    productName: materialNames[2001],
    qualifiedQty: 59,
    versionId: 32,
  },
  {
    batchNo: 'MX200-20260721-C',
    finishQty: 48,
    inboundId: 904,
    inboundTime: '2026-07-21T17:40:00',
    materialId: 2002,
    operatorId: 1,
    orderId: 5010,
    productName: materialNames[2002],
    qualifiedQty: 48,
    versionId: 22,
  },
  {
    batchNo: 'AX100-20260727-A',
    finishQty: 30,
    inboundId: 901,
    inboundTime: '2026-07-27T14:30:00',
    materialId: 2001,
    operatorId: 1,
    orderId: 7008,
    productName: materialNames[2001],
    qualifiedQty: 29,
    versionId: 32,
  },
  {
    batchNo: 'MX200-20260726-B',
    finishQty: 20,
    inboundId: 902,
    inboundTime: '2026-07-26T16:10:00',
    materialId: 2002,
    operatorId: 1,
    orderId: 7006,
    productName: materialNames[2002],
    qualifiedQty: 20,
    versionId: 22,
  },
]

const productionOrders: InventoryReferenceData['productionOrders'] = [
  {
    finishedQty: 30,
    materialId: 2001,
    materialName: materialNames[2001]!,
    orderId: 7008,
    planQty: 100,
    remainingQty: 70,
    status: 'in_progress',
    versionId: 32,
    versionNo: 'AX100-V3.2',
  },
  {
    finishedQty: 20,
    materialId: 2002,
    materialName: materialNames[2002]!,
    orderId: 7006,
    planQty: 50,
    remainingQty: 30,
    status: 'in_progress',
    versionId: 22,
    versionNo: 'MX200-V2.2',
  },
  {
    finishedQty: 0,
    materialId: 2001,
    materialName: materialNames[2001]!,
    orderId: 7012,
    planQty: 120,
    remainingQty: 120,
    status: 'pending_schedule',
    versionId: 32,
    versionNo: 'AX100-V3.2',
  },
]

export interface InventoryMockState {
  alerts: InventoryAlertItem[]
  inbounds: CompletionInboundItem[]
  locks: StockLockItem[]
  materialMetadata?: typeof materialMetadata
  obsoleteItems: ObsoleteMaterialItem[]
  productionOrders?: InventoryReferenceData['productionOrders']
  stockByMaterial: Record<number, number>
}

export function snapshotInventoryMock(): InventoryMockState {
  return structuredClone({
    alerts,
    inbounds,
    locks,
    materialMetadata,
    obsoleteItems,
    productionOrders,
    stockByMaterial,
  })
}

export function restoreInventoryMock(state: InventoryMockState) {
  Object.keys(stockByMaterial).forEach((key) => delete stockByMaterial[Number(key)])
  Object.assign(stockByMaterial, structuredClone(state.stockByMaterial))
  if (state.materialMetadata) {
    Object.keys(materialMetadata).forEach((key) => delete materialMetadata[Number(key)])
    Object.assign(materialMetadata, structuredClone(state.materialMetadata))
  }
  if (state.productionOrders) {
    productionOrders.splice(0, productionOrders.length, ...structuredClone(state.productionOrders))
  }
  alerts.splice(0, alerts.length, ...structuredClone(state.alerts))
  inbounds.splice(0, inbounds.length, ...structuredClone(state.inbounds))
  locks.splice(0, locks.length, ...structuredClone(state.locks))
  obsoleteItems.splice(0, obsoleteItems.length, ...structuredClone(state.obsoleteItems))
}

function delay<TResult>(factory: () => TResult) {
  return new Promise<TResult>((resolve, reject) => {
    globalThis.setTimeout(() => {
      try {
        resolve(factory())
      } catch (error) {
        reject(error)
      }
    }, 180)
  })
}

function paginate<TItem>(items: TItem[], page: number, pageSize: number): PageResult<TItem> {
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

function includesDate(value: string, start?: string, end?: string) {
  return (!start || value >= start) && (!end || value <= `${end}T23:59:59`)
}

function getInTransitQty(materialId: number) {
  if (materialId === 1002) {
    return 10
  }
  if (materialId === 1001) {
    return 20
  }
  return 0
}

function getSafetyStock(materialId: number) {
  if (materialId === 1004) {
    return 200
  }
  return 30
}

function getAlertCandidateIds(materialId?: number) {
  if (materialId !== undefined) {
    return [materialId]
  }
  return [1001, 1002, 2002]
}

function getAlertThreshold(materialId: number) {
  if (materialId === 2002) {
    return 20
  }
  return 40
}

function requirePositiveQuantity(value: number, fieldName: string) {
  if (!Number.isFinite(value) || value <= 0) {
    throw new Error(`${fieldName}必须是大于 0 的有效数量`)
  }
}

function getLockedQty(materialId: number) {
  return locks
    .filter((item) => item.materialId === materialId && item.status === 'locked')
    .reduce((total, item) => total + item.lockQty, 0)
}

function toStockItem(materialId: number): InventoryStockItem {
  const metadata = materialMetadata[materialId] ?? {
    materialType: 'raw_material' as const,
    safetyStock: 0,
    unit: '件',
  }
  const availableQty = stockByMaterial[materialId] ?? 0
  const lockedQty = getLockedQty(materialId)
  let status: InventoryStockItem['status'] = 'normal'
  if (availableQty <= 0) {
    status = 'zero'
  } else if (availableQty < metadata.safetyStock) {
    status = 'low'
  } else if (lockedQty > 0) {
    status = 'locked'
  }
  return {
    availableQty,
    lastInDate: metadata.lastInDate,
    lastOutDate: metadata.lastOutDate,
    lockedQty,
    materialId,
    materialName: materialNames[materialId] ?? `物料 #${materialId}`,
    materialType: metadata.materialType,
    safetyStock: metadata.safetyStock,
    status,
    unit: metadata.unit,
  }
}

function consumeOrderLocks(
  form: CompletionInboundFormData,
  productionOrder: InventoryReferenceData['productionOrders'][number],
  inboundTime: string,
) {
  const consumedLockRecords: StockLockItem[] = []
  let nextLockId = Math.max(500, ...locks.map(({ lockId }) => lockId)) + 1
  const orderLocks = locks.filter(
    (item) => item.orderId === form.orderId && item.status === 'locked',
  )
  for (const lock of orderLocks) {
    const proportionalQty = Number(
      ((lock.lockQty * form.finishQty) / productionOrder.remainingQty).toFixed(2),
    )
    const consumedQty = Math.min(lock.lockQty, Math.max(0, proportionalQty))
    if (consumedQty > 0 && consumedQty >= lock.lockQty) {
      Object.assign(lock, { releaseTime: inboundTime, status: 'consumed' as const })
      consumedLockRecords.push(structuredClone(lock))
    } else if (consumedQty > 0) {
      lock.lockQty = Number((lock.lockQty - consumedQty).toFixed(2))
      const consumedRecord: StockLockItem = {
        ...lock,
        lockId: nextLockId++,
        lockQty: consumedQty,
        releaseTime: inboundTime,
        status: 'consumed',
      }
      consumedLockRecords.push(consumedRecord)
      locks.push(consumedRecord)
    }
  }
  return consumedLockRecords
}

function updateProductionOrderAfterInbound(
  productionOrder: InventoryReferenceData['productionOrders'][number],
  finishQty: number,
) {
  productionOrder.finishedQty += finishQty
  productionOrder.remainingQty = Math.max(0, productionOrder.planQty - productionOrder.finishedQty)
  if (productionOrder.remainingQty === 0) {
    productionOrder.status = 'completed'
  }
}

export const inventoryMock = {
  addCompletionInbound(form: CompletionInboundFormData) {
    return delay(() => {
      requirePositiveQuantity(form.finishQty, '完工数量')
      if (!Number.isFinite(form.qualifiedQty) || form.qualifiedQty < 0) {
        throw new Error('合格数量不能小于 0')
      }
      if (form.qualifiedQty > form.finishQty) {
        throw new Error('合格数量不能大于完工数量')
      }
      if (inbounds.some((item) => item.batchNo === form.batchNo)) {
        throw new Error('该批次号已登记，请勿重复入库')
      }
      const productionOrder = productionOrders.find((item) => item.orderId === form.orderId)
      if (
        !productionOrder ||
        productionOrder.materialId !== form.materialId ||
        productionOrder.versionId !== form.versionId
      ) {
        throw new Error('生产订单、成品物料与 BOM 版本不匹配')
      }
      if (form.finishQty > productionOrder.remainingQty) {
        throw new Error(`本次完工数量超过订单剩余数量 ${productionOrder.remainingQty}`)
      }
      const inboundTime = new Date().toISOString()
      const consumedLockRecords = consumeOrderLocks(form, productionOrder, inboundTime)
      const item: CompletionInboundItem = {
        ...form,
        consumedLockRecords,
        inboundId: Math.max(900, ...inbounds.map(({ inboundId }) => inboundId)) + 1,
        inboundTime,
        productName: materialNames[form.materialId] ?? `物料 #${form.materialId}`,
      }
      inbounds = [item, ...inbounds]
      stockByMaterial[form.materialId] = (stockByMaterial[form.materialId] ?? 0) + form.qualifiedQty
      updateProductionOrderAfterInbound(productionOrder, form.finishQty)
      const metadata = materialMetadata[form.materialId]
      if (metadata) {
        metadata.lastInDate = inboundTime.slice(0, 10)
      }
      return { ...item }
    })
  },

  calculateShortage(items: MaterialShortageRequestItem[]): Promise<MaterialShortageResult> {
    return delay(() => {
      if (items.length === 0) {
        throw new Error('至少需要一条生产需求')
      }
      items.forEach((item) => {
        requirePositiveQuantity(item.productionQty, '生产数量')
        if (!Number.isInteger(item.materialId) || item.materialId <= 0) {
          throw new Error('生产物料不能为空')
        }
        if (!Number.isInteger(item.versionId) || item.versionId <= 0) {
          throw new Error('BOM 版本不能为空')
        }
      })
      const requirements = new Map<
        number,
        { grossRequirement: number; level: number; parentMaterialId: number }
      >()

      function expandComponents(
        components: MockBomComponent[],
        context: {
          level: number
          parentMaterialId: number
          parentRequirement: number
          path: ReadonlySet<number>
        },
      ) {
        const { level, parentMaterialId, parentRequirement, path } = context
        for (const component of components) {
          if (path.has(component.materialId)) {
            throw new Error(`BOM 存在循环引用：物料 #${component.materialId}`)
          }
          const grossRequirement = parentRequirement * component.quantity * (1 + component.lossRate)
          const existing = requirements.get(component.materialId)
          requirements.set(component.materialId, {
            grossRequirement: (existing?.grossRequirement ?? 0) + grossRequirement,
            level: Math.min(existing?.level ?? level, level),
            parentMaterialId: existing?.parentMaterialId ?? parentMaterialId,
          })
          if (component.children?.length) {
            const nextPath = new Set(path)
            nextPath.add(component.materialId)
            expandComponents(component.children, {
              level: level + 1,
              parentMaterialId: component.materialId,
              parentRequirement: grossRequirement,
              path: nextPath,
            })
          }
        }
      }

      for (const request of items) {
        const bom = mockBomVersions[request.versionId]
        if (!bom || bom.materialId !== request.materialId) {
          throw new Error(`物料 #${request.materialId} 与 BOM 版本 #${request.versionId} 不匹配`)
        }
        expandComponents(bom.components, {
          level: 1,
          parentMaterialId: request.materialId,
          parentRequirement: request.productionQty,
          path: new Set([request.materialId]),
        })
      }
      const resultItems = [...requirements.entries()].map(([materialId, requirement]) => {
        const grossRequirement = Number(requirement.grossRequirement.toFixed(2))
        const availableQty = stockByMaterial[materialId] ?? 0
        const inTransitQty = getInTransitQty(materialId)
        const safetyStock = getSafetyStock(materialId)
        const netShortageQty = Math.max(
          0,
          Number((grossRequirement - availableQty - inTransitQty + safetyStock).toFixed(2)),
        )
        return {
          availableQty,
          grossRequirement,
          inTransitQty,
          level: requirement.level,
          materialId,
          materialName: materialNames[materialId],
          netShortageQty,
          parentMaterialId: requirement.parentMaterialId,
          safetyStock,
          suggestedPurchaseQty: netShortageQty,
        }
      })
      return { calculatedAt: new Date().toISOString(), items: resultItems }
    })
  },

  detectObsolete(idleDaysThreshold: number, materialId?: number): Promise<ObsoleteDetectionResult> {
    return delay(() => {
      const candidateId = materialId ?? 1001
      const existing = obsoleteItems.find(
        (item) => item.materialId === candidateId && item.status === 'pending',
      )
      if (existing) {
        return { detectedCount: 1, items: [existing] }
      }
      const item: ObsoleteMaterialItem = {
        activeOrderIds: [],
        availableQty: stockByMaterial[candidateId] ?? 0,
        bomVersionIds: [],
        detectTime: new Date().toISOString(),
        detectionId: Math.max(800, ...obsoleteItems.map(({ detectionId }) => detectionId)) + 1,
        idleDays: idleDaysThreshold + 18,
        lastOutDate: '2026-03-18',
        materialId: candidateId,
        materialName: materialNames[candidateId] ?? `物料 #${candidateId}`,
        status: 'pending',
      }
      obsoleteItems = [item, ...obsoleteItems]
      return { detectedCount: 1, items: [item] }
    })
  },

  generateAlerts(materialId?: number): Promise<InventoryAlertGenerateResult> {
    return delay(() => {
      const candidates = getAlertCandidateIds(materialId)
      const generated: InventoryAlertItem[] = []
      let skippedPendingCount = 0
      for (const candidate of candidates) {
        const hasPendingAlert = alerts.some(
          (item) => item.materialId === candidate && item.status === 'pending',
        )
        if (hasPendingAlert) {
          skippedPendingCount += 1
        } else {
          const threshold = getAlertThreshold(candidate)
          const availableQty = stockByMaterial[candidate] ?? 0
          if (availableQty < threshold) {
            const item: InventoryAlertItem = {
              alertId:
                Math.max(300, ...alerts.map(({ alertId }) => alertId)) + generated.length + 1,
              alertTime: new Date().toISOString(),
              alertType: 'low_stock',
              availableQty,
              materialId: candidate,
              materialName: materialNames[candidate] ?? `物料 #${candidate}`,
              status: 'pending',
              threshold,
            }
            generated.push(item)
          }
        }
      }
      alerts = [...generated, ...alerts]
      return { generatedCount: generated.length, items: generated, skippedPendingCount }
    })
  },

  getReferenceData() {
    return delay(() => ({
      bomVersions: Object.entries(mockBomVersions).map(([versionId, version]) => ({
        materialId: version.materialId,
        versionId: Number(versionId),
        versionNo: version.versionNo,
      })),
      materials: Object.keys(materialNames).map((value) => {
        const materialId = Number(value)
        const metadata = materialMetadata[materialId]!
        return {
          materialId,
          materialName: materialNames[materialId]!,
          materialType: metadata.materialType,
          unit: metadata.unit,
        }
      }),
      productionOrders: structuredClone(productionOrders),
    }))
  },

  handleAlert(alertId: number, status: 'handled' | 'ignored', handlerId: number) {
    return delay(() => {
      const item = alerts.find((alert) => alert.alertId === alertId)
      if (!item || item.status !== 'pending') {
        throw new Error('该库存预警不存在或已处理')
      }
      Object.assign(item, { handleTime: new Date().toISOString(), handlerId, status })
      return { ...item }
    })
  },

  handleObsolete(detectionId: number, status: 'handled' | 'ignored', handlerId: number) {
    return delay(() => {
      const item = obsoleteItems.find(({ detectionId: id }) => id === detectionId)
      if (!item || item.status !== 'pending') {
        throw new Error('该检测记录不存在或已处理')
      }
      Object.assign(item, { handlerId, status })
      return { ...item }
    })
  },

  listAlerts(query: InventoryAlertQuery) {
    return delay(() =>
      paginate(
        alerts.filter(
          (item) =>
            (!query.materialId || item.materialId === query.materialId) &&
            (!query.status || item.status === query.status) &&
            includesDate(item.alertTime, query.startTime, query.endTime),
        ),
        query.page,
        query.pageSize,
      ),
    )
  },

  listCompletionInbound(query: CompletionInboundQuery) {
    return delay(() =>
      paginate(
        inbounds.filter(
          (item) =>
            (!query.orderId || item.orderId === query.orderId) &&
            (!query.materialId || item.materialId === query.materialId) &&
            includesDate(item.inboundTime, query.startTime, query.endTime),
        ),
        query.page,
        query.pageSize,
      ),
    )
  },

  listLocks(query: StockLockQuery) {
    return delay(() =>
      paginate(
        locks.filter(
          (item) =>
            (!query.orderId || item.orderId === query.orderId) &&
            (!query.materialId || item.materialId === query.materialId) &&
            (!query.status || item.status === query.status),
        ),
        query.page,
        query.pageSize,
      ),
    )
  },

  listObsolete(query: ObsoleteMaterialQuery) {
    return delay(() =>
      paginate(
        obsoleteItems.filter(
          (item) =>
            (!query.materialId || item.materialId === query.materialId) &&
            (!query.status || item.status === query.status) &&
            includesDate(item.detectTime, query.startTime, query.endTime),
        ),
        query.page,
        query.pageSize,
      ),
    )
  },

  listStocks(query: InventoryStockQuery) {
    return delay(() =>
      paginate(
        Object.keys(stockByMaterial)
          .map(Number)
          .map(toStockItem)
          .filter(
            (item) =>
              (!query.materialId || item.materialId === query.materialId) &&
              (!query.materialName ||
                item.materialName.toLowerCase().includes(query.materialName.toLowerCase())) &&
              (!query.materialType || item.materialType === query.materialType) &&
              (!query.status || item.status === query.status),
          ),
        query.page,
        query.pageSize,
      ),
    )
  },

  lockStock(form: StockLockFormData): Promise<StockLockResult> {
    return delay(() => {
      if (!form.items.length) {
        throw new Error('至少需要选择一条物料')
      }
      form.items.forEach((item) => requirePositiveQuantity(item.lockQty, '锁定数量'))
      const shortages = form.items
        .filter((item) => item.lockQty > (stockByMaterial[item.materialId] ?? 0))
        .map((item) => ({
          availableQty: stockByMaterial[item.materialId] ?? 0,
          materialId: item.materialId,
          requiredQty: item.lockQty,
          shortageQty: item.lockQty - (stockByMaterial[item.materialId] ?? 0),
        }))
      if (shortages.length) {
        return { items: [], shortages, success: false }
      }
      const created = form.items.map((formItem, index) => ({
        lockId: Math.max(500, ...locks.map(({ lockId }) => lockId)) + index + 1,
        lockQty: formItem.lockQty,
        lockTime: new Date().toISOString(),
        materialId: formItem.materialId,
        materialName: materialNames[formItem.materialId] ?? `物料 #${formItem.materialId}`,
        operatorId: form.operatorId,
        orderId: form.orderId,
        status: 'locked' as const,
      }))
      created.forEach((item) => {
        stockByMaterial[item.materialId] = (stockByMaterial[item.materialId] ?? 0) - item.lockQty
      })
      locks = [...created, ...locks]
      return { items: created, shortages: [], success: true }
    })
  },

  releaseLock(lockId: number, _operatorId: number) {
    return delay(() => {
      const item = locks.find((lock) => lock.lockId === lockId)
      if (!item || item.status !== 'locked') {
        throw new Error('该库存锁定记录不可释放')
      }
      item.status = 'cancelled'
      item.releaseTime = new Date().toISOString()
      stockByMaterial[item.materialId] = (stockByMaterial[item.materialId] ?? 0) + item.lockQty
      return { ...item }
    })
  },
}
