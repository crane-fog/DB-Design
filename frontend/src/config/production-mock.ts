import type {
  CapacityConfigFormData,
  CapacityConfigItem,
  CapacityConfigQuery,
  ExternalOrderItem,
  ExternalOrderQuery,
  FaultRecordItem,
  FaultReportFormData,
  FaultUpdateFormData,
  LineTypeFormData,
  LineTypeItem,
  LineTypeQuery,
  ProductionCalendarFormData,
  ProductionCalendarItem,
  ProductionCalendarQuery,
  ProductionLineFormData,
  ProductionLineItem,
  ProductionLineQuery,
  ProductionOrderFormData,
  ProductionOrderItem,
  ProductionOrderQuery,
} from '@/services/ProductionService'
import type { PageResult } from '@/services/pagination'

const materialNames: Record<number, string> = {
  2001: '智能控制终端 AX100',
  2002: '模块化执行器 MX200',
}

const typeNames: Record<number, string> = {
  1: '总装线',
  2: 'SMT 线',
  3: '钣金线',
  4: '包装线',
}

const productionOrders: ProductionOrderItem[] = [
  {
    actualEnd: undefined,
    actualStart: undefined,
    finishedQty: 0,
    materialId: 2001,
    materialName: materialNames[2001],
    orderId: 5001,
    planEnd: '2026-07-31T18:00:00',
    planQty: 120,
    planStart: '2026-07-28T08:00:00',
    reviewComment: '待生产计划审核',
    status: 'pending_review',
    versionId: 32,
    versionNo: 'V3.2',
  },
  {
    actualEnd: undefined,
    actualStart: undefined,
    finishedQty: 0,
    materialId: 2002,
    materialName: materialNames[2002],
    orderId: 5002,
    planEnd: '2026-08-02T18:00:00',
    planQty: 80,
    planStart: '2026-07-29T08:00:00',
    status: 'pending_schedule',
    versionId: 22,
    versionNo: 'V2.2',
  },
  {
    actualEnd: undefined,
    actualStart: '2026-07-27T08:10:00',
    finishedQty: 46,
    materialId: 2001,
    materialName: materialNames[2001],
    orderId: 5003,
    planEnd: '2026-07-30T18:00:00',
    planQty: 100,
    planStart: '2026-07-27T08:00:00',
    status: 'in_progress',
    versionId: 32,
    versionNo: 'V3.2',
  },
  {
    actualEnd: undefined,
    actualStart: '2026-07-25T08:00:00',
    finishedQty: 65,
    materialId: 2002,
    materialName: materialNames[2002],
    orderId: 5004,
    planEnd: '2026-07-29T18:00:00',
    planQty: 80,
    planStart: '2026-07-25T08:00:00',
    status: 'in_progress',
    versionId: 22,
    versionNo: 'V2.2',
  },
  {
    actualEnd: '2026-07-24T16:20:00',
    actualStart: '2026-07-22T08:00:00',
    finishedQty: 60,
    materialId: 2001,
    materialName: materialNames[2001],
    orderId: 5005,
    planEnd: '2026-07-25T18:00:00',
    planQty: 60,
    planStart: '2026-07-22T08:00:00',
    status: 'completed',
    versionId: 32,
    versionNo: 'V3.2',
  },
  {
    actualEnd: undefined,
    actualStart: undefined,
    finishedQty: 0,
    materialId: 2002,
    materialName: materialNames[2002],
    orderId: 5006,
    planEnd: '2026-07-28T18:00:00',
    planQty: 50,
    planStart: '2026-07-24T08:00:00',
    reviewComment: '客户配置待确认',
    status: 'cancelled',
    versionId: 22,
    versionNo: 'V2.2',
  },
  {
    actualEnd: undefined,
    actualStart: undefined,
    finishedQty: 0,
    materialId: 2001,
    materialName: materialNames[2001],
    orderId: 5007,
    planEnd: '2026-08-04T18:00:00',
    planQty: 140,
    planStart: '2026-08-01T08:00:00',
    status: 'pending_review',
    versionId: 32,
    versionNo: 'V3.2',
  },
  {
    actualEnd: undefined,
    actualStart: undefined,
    finishedQty: 0,
    materialId: 2002,
    materialName: materialNames[2002],
    orderId: 5008,
    planEnd: '2026-08-05T18:00:00',
    planQty: 90,
    planStart: '2026-08-02T08:00:00',
    status: 'pending_schedule',
    versionId: 22,
    versionNo: 'V2.2',
  },
  {
    actualEnd: undefined,
    actualStart: '2026-07-28T08:05:00',
    finishedQty: 18,
    materialId: 2001,
    materialName: materialNames[2001],
    orderId: 5009,
    planEnd: '2026-08-01T18:00:00',
    planQty: 72,
    planStart: '2026-07-28T08:00:00',
    status: 'in_progress',
    versionId: 32,
    versionNo: 'V3.2',
  },
  {
    actualEnd: '2026-07-21T17:10:00',
    actualStart: '2026-07-18T08:00:00',
    finishedQty: 48,
    materialId: 2002,
    materialName: materialNames[2002],
    orderId: 5010,
    planEnd: '2026-07-22T18:00:00',
    planQty: 48,
    planStart: '2026-07-18T08:00:00',
    status: 'completed',
    versionId: 22,
    versionNo: 'V2.2',
  },
  {
    actualEnd: undefined,
    actualStart: undefined,
    finishedQty: 0,
    materialId: 2001,
    materialName: materialNames[2001],
    orderId: 5011,
    planEnd: '2026-08-08T18:00:00',
    planQty: 160,
    planStart: '2026-08-05T08:00:00',
    status: 'pending_review',
    versionId: 32,
    versionNo: 'V3.2',
  },
  {
    actualEnd: undefined,
    actualStart: undefined,
    finishedQty: 0,
    materialId: 2002,
    materialName: materialNames[2002],
    orderId: 5012,
    planEnd: '2026-08-09T18:00:00',
    planQty: 100,
    planStart: '2026-08-06T08:00:00',
    status: 'pending_schedule',
    versionId: 22,
    versionNo: 'V2.2',
  },
]

const lineTypes: LineTypeItem[] = Object.entries(typeNames).map(([typeId, typeName]) => ({
  typeId: Number(typeId),
  typeName,
}))

const lines: ProductionLineItem[] = [
  {
    lineId: 101,
    managerId: 1,
    managerName: '张伟',
    startDate: '2025-01-10',
    status: 'running',
    typeId: 1,
    typeName: typeNames[1],
  },
  {
    lineId: 102,
    managerId: 2,
    managerName: '李娜',
    startDate: '2025-02-18',
    status: 'running',
    typeId: 2,
    typeName: typeNames[2],
  },
  {
    lineId: 103,
    managerId: 3,
    managerName: '王强',
    startDate: '2025-03-06',
    status: 'idle',
    typeId: 3,
    typeName: typeNames[3],
  },
  {
    lineId: 104,
    managerId: 4,
    managerName: '赵敏',
    startDate: '2025-03-20',
    status: 'fault',
    typeId: 4,
    typeName: typeNames[4],
  },
  {
    lineId: 105,
    managerId: 5,
    managerName: '陈晨',
    startDate: '2025-04-12',
    status: 'running',
    typeId: 1,
    typeName: typeNames[1],
  },
  {
    lineId: 106,
    managerId: 6,
    managerName: '周凯',
    startDate: '2025-05-08',
    status: 'idle',
    typeId: 2,
    typeName: typeNames[2],
  },
  {
    lineId: 107,
    managerId: 7,
    managerName: '吴越',
    startDate: '2025-05-26',
    status: 'running',
    typeId: 3,
    typeName: typeNames[3],
  },
  {
    lineId: 108,
    managerId: 8,
    managerName: '孙宁',
    startDate: '2025-06-14',
    status: 'idle',
    typeId: 4,
    typeName: typeNames[4],
  },
  {
    lineId: 109,
    managerId: 9,
    managerName: '刘洋',
    startDate: '2025-06-21',
    status: 'running',
    typeId: 1,
    typeName: typeNames[1],
  },
  {
    lineId: 110,
    managerId: 10,
    managerName: '高峰',
    startDate: '2025-06-30',
    status: 'running',
    typeId: 2,
    typeName: typeNames[2],
  },
  {
    lineId: 111,
    managerId: 11,
    managerName: '何静',
    startDate: '2025-07-05',
    status: 'idle',
    typeId: 3,
    typeName: typeNames[3],
  },
  {
    lineId: 112,
    managerId: 12,
    managerName: '杨雪',
    startDate: '2025-07-12',
    status: 'running',
    typeId: 4,
    typeName: typeNames[4],
  },
]

const capacityConfigs: CapacityConfigItem[] = productionOrders.slice(0, 12).map((order, index) => {
  let unitTime = 24 + (index % 3) * 2
  if (order.materialId === 2001) {
    unitTime = 18 + (index % 3) * 2
  }
  return {
    configId: 200 + index,
    materialId: order.materialId,
    materialName: order.materialName,
    typeId: (index % 4) + 1,
    typeName: typeNames[(index % 4) + 1],
    unitTime,
  }
})

const calendars: ProductionCalendarItem[] = Array.from({ length: 12 }, (_unused, index) => {
  const line = lines[index]
  const config = capacityConfigs[index]
  if (!line || !config) {
    throw new Error('生产 Mock 排产数据配置不完整')
  }
  return {
    calendarDate: `2026-08-${String(index + 1).padStart(2, '0')}`,
    configId: config.configId,
    lineId: line.lineId,
    lineName: `生产线 ${line.lineId}`,
    materialId: config.materialId,
    materialName: config.materialName,
    typeId: config.typeId,
    typeName: config.typeName,
  }
})

const externalOrders: ExternalOrderItem[] = [
  {
    contactPerson: '刘经理',
    contactPhone: '13800001201',
    customerId: 301,
    customerName: '华南设备',
    expectedDate: '2026-08-05',
    extOrderId: 9001,
    materialId: 2001,
    materialName: materialNames[2001],
    quantity: 80,
    status: 'accepted',
    submitTime: '2026-07-25T10:00:00',
  },
  {
    contactPerson: '周工',
    contactPhone: '13900001202',
    customerId: 302,
    customerName: '远洋自动化',
    expectedDate: '2026-08-08',
    extOrderId: 9002,
    materialId: 2002,
    materialName: materialNames[2002],
    quantity: 60,
    status: 'pending_review',
    submitTime: '2026-07-26T14:20:00',
  },
]

const faultRecords: FaultRecordItem[] = [
  {
    description: '生产线传感器信号异常',
    faultId: 8001,
    faultType: '设备异常',
    lineId: 104,
    occurTime: '2026-07-28T09:20:00',
    reporterId: 1,
    status: 'pending_repair',
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

function requirePositive(value: number, label: string) {
  if (!Number.isFinite(value) || value <= 0) {
    throw new Error(`${label}必须是大于 0 的有效数值`)
  }
}

function getOrderRecord(orderId: number) {
  const order = productionOrders.find((item) => item.orderId === orderId)
  if (!order) {
    throw new Error('未找到生产订单')
  }
  return order
}

function getLineRecord(lineId: number) {
  const line = lines.find((item) => item.lineId === lineId)
  if (!line) {
    throw new Error('未找到生产线')
  }
  return line
}

function getConfigRecord(configId: number) {
  const config = capacityConfigs.find((item) => item.configId === configId)
  if (!config) {
    throw new Error('未找到产能配置')
  }
  return config
}

function timestamp() {
  return new Date().toISOString()
}

function approveOrder(orderId: number, approved: boolean, reviewComment?: string) {
  return delay(() => {
    const order = getOrderRecord(orderId)
    if (order.status !== 'pending_review') {
      throw new Error('当前订单状态不可审核')
    }
    order.status = 'cancelled'
    if (approved) {
      order.status = 'pending_schedule'
    }
    order.reviewComment = reviewComment?.trim() || undefined
    return structuredClone(order)
  })
}

function cancelOrder(orderId: number, remark?: string) {
  return delay(() => {
    const order = getOrderRecord(orderId)
    if (order.status === 'completed' || order.status === 'cancelled') {
      throw new Error('当前订单不可取消')
    }
    order.status = 'cancelled'
    order.reviewComment = remark?.trim() || order.reviewComment
    return structuredClone(order)
  })
}

function createOrder(form: ProductionOrderFormData) {
  return delay(() => {
    requirePositive(form.materialId, '物料 ID')
    requirePositive(form.versionId, 'BOM 版本 ID')
    requirePositive(form.planQty, '计划数量')
    if (!materialNames[form.materialId]) {
      throw new Error('物料不存在')
    }
    const order: ProductionOrderItem = {
      actualEnd: undefined,
      actualStart: undefined,
      finishedQty: 0,
      materialId: form.materialId,
      materialName: materialNames[form.materialId],
      orderId: Math.max(...productionOrders.map((item) => item.orderId), 5000) + 1,
      planEnd: form.planEnd,
      planQty: form.planQty,
      planStart: form.planStart,
      status: 'pending_review',
      versionId: form.versionId,
      versionNo: `V${form.versionId}`,
    }
    productionOrders.unshift(order)
    return structuredClone(order)
  })
}

function updateOrder(orderId: number, form: ProductionOrderFormData) {
  return delay(() => {
    const order = getOrderRecord(orderId)
    if (order.status !== 'pending_review' && order.status !== 'pending_schedule') {
      throw new Error('当前订单状态不可修改')
    }
    requirePositive(form.materialId, '物料 ID')
    requirePositive(form.versionId, 'BOM 版本 ID')
    requirePositive(form.planQty, '计划数量')
    if (!materialNames[form.materialId]) {
      throw new Error('物料不存在')
    }
    Object.assign(order, {
      materialId: form.materialId,
      materialName: materialNames[form.materialId],
      planEnd: form.planEnd,
      planQty: form.planQty,
      planStart: form.planStart,
      versionId: form.versionId,
      versionNo: `V${form.versionId}`,
    })
    return structuredClone(order)
  })
}

function startOrder(orderId: number) {
  return delay(() => {
    const order = getOrderRecord(orderId)
    if (order.status !== 'pending_schedule') {
      throw new Error('当前订单不可开工')
    }
    order.status = 'in_progress'
    order.actualStart = timestamp()
    return structuredClone(order)
  })
}

function finishOrder(orderId: number, finishedQty: number) {
  return delay(() => {
    const order = getOrderRecord(orderId)
    requirePositive(finishedQty, '完工数量')
    if (order.status !== 'in_progress') {
      throw new Error('当前订单不可完工')
    }
    if (finishedQty > order.planQty) {
      throw new Error('完工数量不能超过计划数量')
    }
    order.finishedQty = finishedQty
    order.status = 'completed'
    order.actualEnd = timestamp()
    return structuredClone(order)
  })
}

function saveCapacityConfig(form: CapacityConfigFormData) {
  return delay(() => {
    requirePositive(form.materialId, '物料 ID')
    requirePositive(form.typeId, '生产线类型 ID')
    requirePositive(form.unitTime, '单位工时')
    if (!materialNames[form.materialId]) {
      throw new Error('物料不存在')
    }
    let existing = undefined as CapacityConfigItem | undefined
    if (form.configId) {
      existing = capacityConfigs.find((item) => item.configId === form.configId)
    }
    const config: CapacityConfigItem = existing ?? {
      configId: Math.max(...capacityConfigs.map((item) => item.configId), 200) + 1,
      materialId: form.materialId,
      materialName: materialNames[form.materialId],
      typeId: form.typeId,
      typeName: typeNames[form.typeId] ?? `生产线类型 #${form.typeId}`,
      unitTime: form.unitTime,
    }
    Object.assign(config, {
      materialId: form.materialId,
      materialName: materialNames[form.materialId],
      typeId: form.typeId,
      typeName: typeNames[form.typeId] ?? `生产线类型 #${form.typeId}`,
      unitTime: form.unitTime,
    })
    if (!existing) {
      capacityConfigs.push(config)
    }
    return structuredClone(config)
  })
}

function createLine(form: ProductionLineFormData) {
  return delay(() => {
    requirePositive(form.managerId, '负责人 ID')
    requirePositive(form.typeId, '生产线类型 ID')
    const line: ProductionLineItem = {
      lineId: Math.max(...lines.map((item) => item.lineId), 100) + 1,
      managerId: form.managerId,
      startDate: form.startDate,
      status: 'idle',
      typeId: form.typeId,
      typeName: typeNames[form.typeId] ?? `生产线类型 #${form.typeId}`,
    }
    lines.push(line)
    return structuredClone(line)
  })
}

function updateLine(lineId: number, form: ProductionLineFormData) {
  return delay(() => {
    const line = getLineRecord(lineId)
    requirePositive(form.managerId, '负责人 ID')
    requirePositive(form.typeId, '生产线类型 ID')
    Object.assign(line, {
      ...form,
      typeName: typeNames[form.typeId] ?? `生产线类型 #${form.typeId}`,
    })
    return structuredClone(line)
  })
}

function saveLineType(form: LineTypeFormData) {
  return delay(() => {
    if (!form.typeName.trim()) {
      throw new Error('生产线类型名称不能为空')
    }
    const typeId = form.typeId ?? Math.max(...lineTypes.map((item) => item.typeId), 0) + 1
    const existing = lineTypes.find((item) => item.typeId === typeId)
    if (existing) {
      existing.typeName = form.typeName.trim()
    } else {
      lineTypes.push({ typeId, typeName: form.typeName.trim() })
    }
    typeNames[typeId] = form.typeName.trim()
    return structuredClone({ typeId, typeName: form.typeName.trim() })
  })
}

function saveCalendar(form: ProductionCalendarFormData) {
  return delay(() => {
    getLineRecord(form.lineId)
    const config = getConfigRecord(form.configId)
    const existing = calendars.find(
      (item) => item.calendarDate === form.calendarDate && item.lineId === form.lineId,
    )
    const item: ProductionCalendarItem = existing ?? {
      calendarDate: form.calendarDate,
      configId: form.configId,
      lineId: form.lineId,
      lineName: `生产线 #${form.lineId}`,
      materialId: config.materialId,
      materialName: config.materialName,
      typeId: config.typeId,
      typeName: config.typeName,
    }
    Object.assign(item, {
      configId: form.configId,
      materialId: config.materialId,
      materialName: config.materialName,
      typeId: config.typeId,
      typeName: config.typeName,
    })
    if (!existing) {
      calendars.push(item)
    }
    return structuredClone(item)
  })
}

function deleteCalendar(calendarDate: string, lineId: number) {
  return delay(() => {
    const index = calendars.findIndex(
      (item) => item.calendarDate === calendarDate && item.lineId === lineId,
    )
    if (index === -1) {
      throw new Error('未找到排产日历')
    }
    calendars.splice(index, 1)
  })
}

function reviewExternalOrder(extOrderId: number, accepted: boolean, reviewComment?: string) {
  return delay(() => {
    const order = externalOrders.find((item) => item.extOrderId === extOrderId)
    if (!order) {
      throw new Error('未找到外部订单')
    }
    if (order.status !== 'pending_review') {
      throw new Error('当前外部订单不可审核')
    }
    order.status = 'rejected'
    if (accepted) {
      order.status = 'accepted'
    }
    order.reviewComment = reviewComment?.trim() || undefined
    return structuredClone(order)
  })
}

function reportFault(form: FaultReportFormData) {
  return delay(() => {
    requirePositive(form.lineId, '生产线 ID')
    getLineRecord(form.lineId)
    if (!form.faultType.trim() || !form.description.trim()) {
      throw new Error('故障类型和描述不能为空')
    }
    const record: FaultRecordItem = {
      description: form.description.trim(),
      faultId: Math.max(...faultRecords.map((item) => item.faultId), 8000) + 1,
      faultType: form.faultType.trim(),
      lineId: form.lineId,
      occurTime: timestamp(),
      reporterId: 1,
      status: 'pending_repair',
    }
    faultRecords.unshift(record)
    return structuredClone(record)
  })
}

function updateFault(form: FaultUpdateFormData) {
  return delay(() => {
    const record = faultRecords.find((item) => item.faultId === form.faultId)
    if (!record) {
      throw new Error('未找到故障记录')
    }
    if (form.status === 'recovered' && !form.recoverTime) {
      throw new Error('故障恢复时必须填写恢复时间')
    }
    record.status = form.status
    record.repairerId = form.repairerId
    record.recoverTime = undefined
    if (form.status === 'recovered') {
      record.recoverTime = form.recoverTime
    }
    return structuredClone(record)
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

function includesDate(value: string, start?: string, end?: string) {
  return (!start || value >= start) && (!end || value <= `${end}T23:59:59`)
}

function listOrders(query: ProductionOrderQuery) {
  return delay(() =>
    paginate(
      productionOrders.filter(
        (order) =>
          (!query.materialId || order.materialId === query.materialId) &&
          (!query.status || order.status === query.status) &&
          includesDate(order.planEnd, query.planEndStart, query.planEndEnd),
      ),
      query.page,
      query.pageSize,
    ),
  )
}

function listConfigs(query: CapacityConfigQuery) {
  return delay(() =>
    paginate(
      capacityConfigs.filter(
        (config) =>
          (!query.materialId || config.materialId === query.materialId) &&
          (!query.typeId || config.typeId === query.typeId),
      ),
      query.page,
      query.pageSize,
    ),
  )
}

function listLines(query: ProductionLineQuery) {
  return delay(() =>
    paginate(
      lines.filter(
        (line) =>
          (!query.status || line.status === query.status) &&
          (!query.typeId || line.typeId === query.typeId),
      ),
      query.page,
      query.pageSize,
    ),
  )
}

function listTypes(query: LineTypeQuery) {
  return delay(() =>
    paginate(
      lineTypes.filter(
        (type) =>
          !query.typeName ||
          type.typeName.toLowerCase().includes(query.typeName.trim().toLowerCase()),
      ),
      query.page,
      query.pageSize,
    ),
  )
}

function listCalendars(query: ProductionCalendarQuery) {
  return delay(() =>
    paginate(
      calendars.filter(
        (calendar) =>
          (!query.lineId || calendar.lineId === query.lineId) &&
          (!query.configId || calendar.configId === query.configId) &&
          includesDate(calendar.calendarDate, query.calendarDateStart, query.calendarDateEnd),
      ),
      query.page,
      query.pageSize,
    ),
  )
}

export const productionMock = {
  approveOrder,
  cancelOrder,
  createLine,
  createOrder,
  deleteCalendar,
  finishOrder,
  getOrder(orderId: number) {
    return delay(() => {
      const order = productionOrders.find((item) => item.orderId === orderId)
      if (!order) {
        throw new Error('未找到生产订单')
      }
      return structuredClone(order)
    })
  },
  listAllLineTypes() {
    return delay(() => structuredClone(lineTypes))
  },
  listCalendars,
  listCapacityConfigs: listConfigs,
  listExternalOrders(query: ExternalOrderQuery) {
    return delay(() =>
      paginate(
        externalOrders.filter(
          (order) =>
            (!query.customerId || order.customerId === query.customerId) &&
            (!query.status || order.status === query.status),
        ),
        query.page,
        query.pageSize,
      ),
    )
  },
  listLineTypes: listTypes,
  listLines,
  listOrders,
  reportFault,
  reviewExternalOrder,
  saveCalendar,
  saveCapacityConfig,
  saveLineType,
  startOrder,
  updateFault,
  updateLine,
  updateOrder,
}

export type ProductionMockWrite =
  | 'approveOrder'
  | 'cancelOrder'
  | 'createLine'
  | 'createOrder'
  | 'deleteCalendar'
  | 'finishOrder'
  | 'reportFault'
  | 'reviewExternalOrder'
  | 'saveCalendar'
  | 'saveCapacityConfig'
  | 'saveLineType'
  | 'startOrder'
  | 'updateFault'
  | 'updateLine'
  | 'updateOrder'
