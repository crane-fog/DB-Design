import type {
  CompletionInboundFormData,
  CompletionInboundItem,
  CompletionInboundQuery,
  InventoryAlertGenerateResult,
  InventoryAlertItem,
  InventoryAlertQuery,
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

const stockByMaterial: Record<number, number> = {
  1001: 36,
  1002: 18,
  1003: 62,
  1004: 520,
  2001: 24,
  2002: 12,
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
    availableQty: 45,
    detectTime: '2026-07-27T07:50:00',
    detectionId: 801,
    idleDays: 128,
    lastOutDate: '2026-03-21',
    materialId: 1003,
    materialName: materialNames[1003],
    status: 'pending',
  },
  {
    availableQty: 80,
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

function getShortageComponentIds(materialId: number) {
  if (materialId === 2002) {
    return [1001, 1003, 1004]
  }
  return [1001, 1002, 1004]
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

export const inventoryMock = {
  addCompletionInbound(form: CompletionInboundFormData) {
    return delay(() => {
      if (form.qualifiedQty > form.finishQty) {
        throw new Error('合格数量不能大于完工数量')
      }
      if (inbounds.some((item) => item.batchNo === form.batchNo)) {
        throw new Error('该批次号已登记，请勿重复入库')
      }
      const inboundTime = new Date().toISOString()
      const consumedLockRecords = locks
        .filter((lock) => lock.orderId === form.orderId && lock.status === 'locked')
        .map((lock) => {
          Object.assign(lock, { releaseTime: inboundTime, status: 'consumed' as const })
          return structuredClone(lock)
        })
      const item: CompletionInboundItem = {
        ...form,
        consumedLockRecords,
        inboundId: Math.max(900, ...inbounds.map(({ inboundId }) => inboundId)) + 1,
        inboundTime,
        productName: materialNames[form.materialId] ?? `物料 #${form.materialId}`,
      }
      inbounds = [item, ...inbounds]
      stockByMaterial[form.materialId] = (stockByMaterial[form.materialId] ?? 0) + form.qualifiedQty
      return { ...item }
    })
  },

  calculateShortage(items: MaterialShortageRequestItem[]): Promise<MaterialShortageResult> {
    return delay(() => ({
      calculatedAt: new Date().toISOString(),
      items: items.flatMap((request, requestIndex) => {
        const componentIds = getShortageComponentIds(request.materialId)
        return componentIds.map((materialId, index) => {
          const grossRequirement = Number((request.productionQty * [2.4, 1, 12][index]!).toFixed(2))
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
            level: requestIndex + 1,
            materialId,
            materialName: materialNames[materialId],
            netShortageQty,
            parentMaterialId: request.materialId,
            safetyStock,
            suggestedPurchaseQty: netShortageQty,
          }
        })
      }),
    }))
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
        availableQty: stockByMaterial[candidateId] ?? 0,
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

  lockStock(form: StockLockFormData): Promise<StockLockResult> {
    return delay(() => {
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
