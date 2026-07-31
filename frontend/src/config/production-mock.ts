import type {
  CapacityBalanceFormData,
  CapacityBalanceItem,
  CapacityConfigFormData,
  CapacityConfigItem,
  CapacityConfigQuery,
  CapacityDetectionFormData,
  CapacityDetectionItem,
  ExternalOrderConvertFormData,
  ExternalOrderConvertItem,
  ExternalOrderCreateFormData,
  ExternalOrderItem,
  ExternalOrderQuery,
  FaultRecordItem,
  FaultRecordQuery,
  FaultReportFormData,
  FaultUpdateFormData,
  LineTypeFormData,
  LineTypeItem,
  LineTypeQuery,
  ProductionCalendarFormData,
  ProductionCalendarItem,
  ProductionCalendarQuery,
  ProductionCapacityEstimateFormData,
  ProductionCapacityEstimateItem,
  ProductionLineFormData,
  ProductionLineItem,
  ProductionLineQuery,
  ProductionLineStatusFormData,
  ProductionLineStatusItem,
  ProductionOrderFormData,
  ProductionOrderItem,
  ProductionOrderQuery,
  ProductionProgressReportFormData,
  ProductionScheduleFormData,
  ProductionScheduleItem,
  ProductionStageItem,
  ProductionStageStatus,
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
    status: 'fault',
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
    faultLevel: 'major',
    faultType: '设备异常',
    lineId: 104,
    lineName: '生产线 104',
    occurTime: '2026-07-28T09:20:00',
    reporterId: 1,
    reporterName: '张伟',
    status: 'pending_repair',
  },
  {
    description: '治具定位偏差，已完成校准。',
    faultId: 8002,
    faultLevel: 'minor',
    faultType: '工装异常',
    lineId: 102,
    lineName: '生产线 102',
    occurTime: '2026-07-26T11:10:00',
    processingNote: '更换定位销并完成首件确认。',
    recoverTime: '2026-07-26T13:20:00',
    repairerId: 2,
    repairerName: '李娜',
    reporterId: 5,
    reporterName: '陈晨',
    status: 'recovered',
  },
  {
    description: '总装工位电机温度持续偏高。',
    faultId: 8003,
    faultLevel: 'critical',
    faultType: '设备异常',
    lineId: 101,
    lineName: '生产线 101',
    occurTime: '2026-07-29T15:05:00',
    processingNote: '正在更换散热风机。',
    repairerId: 3,
    repairerName: '王强',
    reporterId: 7,
    reporterName: '吴越',
    status: 'repairing',
  },
]

const orderSchedules: ProductionScheduleItem[] = [
  {
    lineId: 103,
    lineName: '生产线 103',
    orderId: 5002,
    plannedEnd: '2026-07-30T18:00:00',
    plannedStart: '2026-07-29T08:00:00',
    scheduleId: 6001,
  },
  {
    lineId: 106,
    lineName: '生产线 106',
    orderId: 5008,
    plannedEnd: '2026-08-05T18:00:00',
    plannedStart: '2026-08-03T08:00:00',
    scheduleId: 6002,
  },
]

function createOrderStages(order: ProductionOrderItem): ProductionStageItem[] {
  const stages: ProductionStageItem[] = [
    { name: '备料', status: 'pending' },
    { name: '装配生产', status: 'pending' },
    { name: '质量检验', status: 'pending' },
    { name: '完工入库', status: 'pending' },
  ]
  if (order.status === 'pending_schedule') {
    stages[0] = { completedAt: order.planStart, name: '备料', status: 'completed' }
  }
  if (order.status === 'in_progress') {
    stages[0] = { completedAt: order.actualStart, name: '备料', status: 'completed' }
    stages[1] = { name: '装配生产', startedAt: order.actualStart, status: 'in_progress' }
  }
  if (order.status === 'completed') {
    stages[0] = { completedAt: order.actualStart, name: '备料', status: 'completed' }
    stages[1] = { completedAt: order.actualEnd, name: '装配生产', status: 'completed' }
    stages[2] = { completedAt: order.actualEnd, name: '质量检验', status: 'completed' }
    stages[3] = { completedAt: order.actualEnd, name: '完工入库', status: 'completed' }
  }
  if (order.status === 'cancelled') {
    stages[0] = { name: '备料', status: 'paused' }
  }
  return stages
}

const orderStages: Record<number, ProductionStageItem[]> = Object.fromEntries(
  productionOrders.map((order) => [order.orderId, createOrderStages(order)]),
)

const capacityDetections: CapacityDetectionItem[] = []
const capacityBalances: CapacityBalanceItem[] = []
const lineStatuses: ProductionLineStatusItem[] = lines.map((line, index) => {
  const order = productionOrders[index % productionOrders.length]
  const isRunning = line.status === 'running'
  let currentMaterialId = undefined as number | undefined
  let currentOrderId = undefined as number | undefined
  let efficiency = 0
  let finishedQty = 0
  if (isRunning) {
    currentMaterialId = order?.materialId
    currentOrderId = order?.orderId
    efficiency = 0.82 + (index % 4) * 0.03
    finishedQty = order?.finishedQty ?? 0
  }
  return {
    currentMaterialId,
    currentOrderId,
    efficiency,
    finishedQty,
    lineId: line.lineId,
    status: line.status ?? 'idle',
    updatedTime: '2026-07-30T08:00:00',
  }
})

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

function getOrderStagesRecord(order: ProductionOrderItem) {
  const stages = orderStages[order.orderId]
  if (stages) {
    return stages
  }
  const generated = createOrderStages(order)
  orderStages[order.orderId] = generated
  return generated
}

function toOrderWithSchedule(order: ProductionOrderItem) {
  const schedule = orderSchedules.find((item) => item.orderId === order.orderId)
  return structuredClone({ ...order, schedule })
}

function requireValidScheduleRange(plannedStart: string, plannedEnd: string) {
  const start = Date.parse(plannedStart)
  const end = Date.parse(plannedEnd)
  if (Number.isNaN(start) || Number.isNaN(end) || start >= end) {
    throw new Error('计划开始时间必须早于计划结束时间')
  }
  return { end, start }
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
    getOrderStagesRecord(order).splice(0, 4, ...createOrderStages(order))
    return toOrderWithSchedule(order)
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
    getOrderStagesRecord(order).splice(0, 4, ...createOrderStages(order))
    return toOrderWithSchedule(order)
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
    orderStages[order.orderId] = createOrderStages(order)
    return toOrderWithSchedule(order)
  })
}

function addExternalOrder(form: ExternalOrderCreateFormData) {
  return delay(() => {
    requirePositive(form.materialId, '产品物料 ID')
    requirePositive(form.quantity, '订单数量')
    if (!materialNames[form.materialId]) {
      throw new Error('产品不存在')
    }
    if (!form.contactPerson.trim() || !form.contactPhone.trim()) {
      throw new Error('联系人和联系电话不能为空')
    }
    if (!form.customerId) {
      throw new Error('当前 Mock 账号缺少外部客户身份')
    }
    const order: ExternalOrderItem = {
      contactPerson: form.contactPerson.trim(),
      contactPhone: form.contactPhone.trim(),
      customerId: form.customerId,
      customerName: `客户 #${form.customerId}`,
      expectedDate: form.expectedDate,
      extOrderId: Math.max(...externalOrders.map((item) => item.extOrderId), 9000) + 1,
      materialId: form.materialId,
      materialName: materialNames[form.materialId],
      quantity: form.quantity,
      status: 'pending_review',
      submitTime: timestamp(),
    }
    externalOrders.unshift(order)
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

function setLineStatusIdle(lineId: number, finishedQty: number, updatedTime: string) {
  getLineRecord(lineId).status = 'idle'
  const lineStatus = lineStatuses.find((item) => item.lineId === lineId)
  if (!lineStatus) {
    return
  }
  lineStatus.currentMaterialId = undefined
  lineStatus.currentOrderId = undefined
  lineStatus.efficiency = 0
  lineStatus.finishedQty = finishedQty
  lineStatus.status = 'idle'
  lineStatus.updatedTime = updatedTime
}

function startOrder(orderId: number) {
  return delay(() => {
    const order = getOrderRecord(orderId)
    if (order.status !== 'pending_schedule') {
      throw new Error('当前订单不可开工')
    }
    const schedule = orderSchedules.find((item) => item.orderId === orderId)
    if (!schedule) {
      throw new Error('请先为订单分配生产线并保存排产信息')
    }
    order.status = 'in_progress'
    order.actualStart = timestamp()
    const stages = getOrderStagesRecord(order)
    stages[0] = { completedAt: order.actualStart, name: '备料', status: 'completed' }
    stages[1] = { name: '装配生产', startedAt: order.actualStart, status: 'in_progress' }
    const line = getLineRecord(schedule.lineId)
    line.status = 'running'
    const lineStatus = lineStatuses.find((item) => item.lineId === schedule.lineId)
    if (lineStatus) {
      lineStatus.currentMaterialId = order.materialId
      lineStatus.currentOrderId = order.orderId
      lineStatus.efficiency = lineStatus.efficiency || 0.85
      lineStatus.finishedQty = order.finishedQty ?? 0
      lineStatus.status = 'running'
      lineStatus.updatedTime = order.actualStart
    }
    return toOrderWithSchedule(order)
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
    getOrderStagesRecord(order).splice(0, 4, ...createOrderStages(order))
    const schedule = orderSchedules.find((item) => item.orderId === orderId)
    if (schedule) {
      setLineStatusIdle(schedule.lineId, order.finishedQty, order.actualEnd)
    }
    return toOrderWithSchedule(order)
  })
}

function reportOrderProgress(form: ProductionProgressReportFormData) {
  return delay(() => {
    const order = getOrderRecord(form.orderId)
    requirePositive(form.completedQty, '本次完成数量')
    if (order.status !== 'in_progress') {
      throw new Error('仅生产中的订单可以上报进度')
    }
    const finishedQty = (order.finishedQty ?? 0) + form.completedQty
    if (finishedQty > order.planQty) {
      throw new Error(`本次上报后累计完成数量 ${finishedQty} 超过计划数量 ${order.planQty}`)
    }
    order.finishedQty = finishedQty
    order.lastProgressRemark = form.remark?.trim() || undefined
    order.lastProgressReportedAt = form.reportedAt || timestamp()
    const stages = getOrderStagesRecord(order)
    let stageStatus: ProductionStageStatus = 'in_progress'
    if (finishedQty === order.planQty) {
      stageStatus = 'completed'
    }
    stages[1] = {
      name: '装配生产',
      startedAt: order.actualStart,
      status: stageStatus,
    }
    if (finishedQty === order.planQty) {
      order.status = 'completed'
      order.actualEnd = form.reportedAt || timestamp()
      stages.splice(0, 4, ...createOrderStages(order))
      const schedule = orderSchedules.find((item) => item.orderId === order.orderId)
      if (schedule) {
        setLineStatusIdle(schedule.lineId, order.finishedQty, order.actualEnd)
      }
    }
    return toOrderWithSchedule(order)
  })
}

function saveOrderSchedule(form: ProductionScheduleFormData) {
  return delay(() => {
    const order = getOrderRecord(form.orderId)
    if (order.status !== 'pending_schedule') {
      throw new Error('仅待排产的生产订单可以进行排产')
    }
    const line = getLineRecord(form.lineId)
    if (line.status === 'fault') {
      throw new Error(`生产线 ${line.lineId} 当前故障，不能用于排产`)
    }
    const { end, start } = requireValidScheduleRange(form.plannedStart, form.plannedEnd)
    const existing = orderSchedules.find((item) => item.orderId === form.orderId)
    const conflict = orderSchedules.find(
      (item) =>
        item.lineId === form.lineId &&
        item.orderId !== form.orderId &&
        start < Date.parse(item.plannedEnd) &&
        end > Date.parse(item.plannedStart),
    )
    if (conflict) {
      throw new Error(
        `生产线 ${line.lineId} 在该时段已安排订单 #${conflict.orderId}（${conflict.plannedStart} 至 ${conflict.plannedEnd}）`,
      )
    }
    const schedule: ProductionScheduleItem = existing ?? {
      lineId: form.lineId,
      lineName: `生产线 ${line.lineId}`,
      orderId: form.orderId,
      plannedEnd: form.plannedEnd,
      plannedStart: form.plannedStart,
      scheduleId: Math.max(...orderSchedules.map((item) => item.scheduleId), 6000) + 1,
    }
    Object.assign(schedule, {
      lineId: form.lineId,
      lineName: `生产线 ${line.lineId}`,
      plannedEnd: form.plannedEnd,
      plannedStart: form.plannedStart,
    })
    if (!existing) {
      orderSchedules.push(schedule)
    }
    order.planStart = form.plannedStart
    order.planEnd = form.plannedEnd
    return structuredClone(schedule)
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

function convertExternalOrder(form: ExternalOrderConvertFormData) {
  return delay(() => {
    const externalOrder = externalOrders.find((item) => item.extOrderId === form.extOrderId)
    if (!externalOrder) {
      throw new Error('未找到外部订单')
    }
    if (externalOrder.status !== 'accepted') {
      throw new Error('仅已接受的外部订单可转换')
    }
    if (!form.productionOrders.length) {
      throw new Error('至少需要一个生产订单')
    }
    const firstOrderId = Math.max(...productionOrders.map((item) => item.orderId), 5000) + 1
    const convertedOrders = form.productionOrders.map((orderForm, index) => {
      requirePositive(orderForm.materialId, '产品物料 ID')
      requirePositive(orderForm.versionId, 'BOM 版本 ID')
      requirePositive(orderForm.planQty, '计划数量')
      const order: ProductionOrderItem = {
        finishedQty: 0,
        materialId: orderForm.materialId,
        materialName: materialNames[orderForm.materialId] ?? `物料 #${orderForm.materialId}`,
        orderId: firstOrderId + index,
        planEnd: orderForm.planEnd,
        planQty: orderForm.planQty,
        planStart: orderForm.planStart,
        status: 'pending_review',
        versionId: orderForm.versionId,
        versionNo: `V${orderForm.versionId}`,
      }
      productionOrders.unshift(order)
      return order
    })
    const result: ExternalOrderConvertItem = {
      associations: convertedOrders.map((order) => ({
        extOrderId: form.extOrderId,
        orderId: order.orderId,
      })),
      extOrderId: form.extOrderId,
      productionOrders: convertedOrders.map((order) => ({
        finishedQty: order.finishedQty,
        materialId: order.materialId,
        materialName: order.materialName,
        orderId: order.orderId,
        planQty: order.planQty,
        status: order.status,
      })),
    }
    return structuredClone(result)
  })
}

interface ResolvedCapacityEstimateInput {
  expectedDate: string
  materialId: number
  planQty: number
}

function resolveCapacityEstimateInput(
  form: ProductionCapacityEstimateFormData,
): ResolvedCapacityEstimateInput {
  if (form.orderId) {
    const order = getOrderRecord(form.orderId)
    return {
      expectedDate: order.planEnd.slice(0, 10),
      materialId: order.materialId,
      planQty: order.planQty,
    }
  }
  if (!form.materialId || !form.versionId || !form.planQty || !form.expectedDate) {
    throw new Error('请提供生产订单，或完整填写产品、BOM、数量和期望日期')
  }
  return {
    expectedDate: form.expectedDate,
    materialId: form.materialId,
    planQty: form.planQty,
  }
}

function getCapacityRiskReasons(input: {
  capacityReady: boolean
  estimatedFinishDate: string
  expectedDate: string
  materialReady: boolean
}) {
  const { capacityReady, estimatedFinishDate, expectedDate, materialReady } = input
  const risks: string[] = []
  if (!materialReady) {
    risks.push('关键物料预计延迟齐套')
  }
  if (!capacityReady) {
    risks.push('可用产能不足')
  }
  if (estimatedFinishDate > expectedDate.slice(0, 10)) {
    risks.push('预计完工晚于期望日期')
  }
  return risks
}

function estimateCapacity(
  form: ProductionCapacityEstimateFormData,
): Promise<ProductionCapacityEstimateItem> {
  return delay(() => {
    const { expectedDate, materialId, planQty } = resolveCapacityEstimateInput(form)
    const config = capacityConfigs.find((item) => item.materialId === materialId)
    if (!config) {
      throw new Error('未找到该产品的产能配置')
    }
    const matchingLines = lines.filter(
      (line) => line.typeId === config.typeId && line.status !== 'fault',
    )
    const requiredWorkMinutes = planQty * config.unitTime
    const availableWorkMinutes = matchingLines.length * 8 * 60 * 5
    const materialReady = materialId !== 2002 || planQty <= 90
    const capacityReady = availableWorkMinutes >= requiredWorkMinutes
    let readyDelay = 2
    if (materialReady) {
      readyDelay = 0
    }
    const dailyCapacity = Math.max(480, availableWorkMinutes / 5)
    const requiredDays = Math.max(1, Math.ceil(requiredWorkMinutes / dailyCapacity))
    const estimated = new Date('2026-07-30T00:00:00')
    estimated.setDate(estimated.getDate() + readyDelay + requiredDays)
    const estimatedFinishDate = estimated.toISOString().slice(0, 10)
    const canDeliverOnTime =
      materialReady && capacityReady && estimatedFinishDate <= expectedDate.slice(0, 10)
    const risks = getCapacityRiskReasons({
      capacityReady,
      estimatedFinishDate,
      expectedDate,
      materialReady,
    })
    let latestMaterialReadyDate = undefined as string | undefined
    if (!materialReady) {
      latestMaterialReadyDate = '2026-08-01'
    }
    return {
      availableWorkMinutes,
      canDeliverOnTime,
      capacityReady,
      estimatedFinishDate,
      latestMaterialReadyDate,
      materialReady,
      requiredWorkMinutes,
      riskReason: risks.join('；') || undefined,
    }
  })
}

function runCapacityDetection(form: CapacityDetectionFormData) {
  return delay(() => {
    const line = getLineRecord(form.lineId)
    const status = lineStatuses.find((item) => item.lineId === form.lineId)
    const planCapacity = 100 + (form.lineId % 5) * 20
    let efficiency = status?.efficiency
    if (efficiency === undefined) {
      efficiency = 0.85
      if (line.status === 'fault') {
        efficiency = 0.42
      }
    }
    const actualCapacity = Math.round(planCapacity * efficiency)
    const diffQty = actualCapacity - planCapacity
    let downtimeMinutes = Math.round((1 - efficiency) * 240)
    let reasonType = 'normal_fluctuation'
    if (line.status === 'fault') {
      downtimeMinutes = 180
      reasonType = 'equipment_fault'
    }
    const detection: CapacityDetectionItem = {
      actualCapacity,
      actualWorkHours: Math.round(40 * efficiency * 10) / 10,
      detectionId: Math.max(0, ...capacityDetections.map((item) => item.detectionId)) + 1,
      diffQty,
      diffRate: Math.round((diffQty / planCapacity) * 10_000) / 10_000,
      downtimeMinutes,
      efficiency,
      lineId: form.lineId,
      periodEnd: form.periodEnd,
      periodStart: form.periodStart,
      planCapacity,
      reasonType,
    }
    capacityDetections.unshift(detection)
    return structuredClone(detection)
  })
}

function saveCapacityBalance(form: CapacityBalanceFormData) {
  return delay(() => {
    if (!form.affectedOrders.length) {
      throw new Error('请至少选择一个受影响生产订单')
    }
    form.affectedOrders.forEach((orderId) => getOrderRecord(orderId))
    const balance: CapacityBalanceItem = {
      adjustTime: timestamp(),
      affectedOrders: [...new Set(form.affectedOrders)],
      afterPlan: structuredClone(form.afterPlan),
      balanceId: Math.max(0, ...capacityBalances.map((item) => item.balanceId)) + 1,
      beforePlan: structuredClone(form.beforePlan),
      operatorId: 1,
    }
    capacityBalances.unshift(balance)
    return structuredClone(balance)
  })
}

function updateLineStatus(form: ProductionLineStatusFormData) {
  return delay(() => {
    const line = getLineRecord(form.lineId)
    if (form.currentOrderId) {
      getOrderRecord(form.currentOrderId)
    }
    if (form.efficiency !== undefined && (form.efficiency < 0 || form.efficiency > 1)) {
      throw new Error('效率必须在 0 到 1 之间')
    }
    let status = lineStatuses.find((item) => item.lineId === form.lineId)
    if (!status) {
      status = {
        efficiency: 0,
        finishedQty: 0,
        lineId: form.lineId,
        status: 'idle',
        updatedTime: timestamp(),
      }
      lineStatuses.push(status)
    }
    Object.assign(status, {
      currentMaterialId: form.currentMaterialId ?? status.currentMaterialId,
      currentOrderId: form.currentOrderId ?? status.currentOrderId,
      efficiency: form.efficiency ?? status.efficiency,
      finishedQty: form.finishedQty ?? status.finishedQty,
      status: form.status,
      updatedTime: timestamp(),
    })
    line.status = form.status
    return structuredClone(status)
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
      faultLevel: form.faultLevel,
      faultType: form.faultType.trim(),
      lineId: form.lineId,
      lineName: `生产线 ${form.lineId}（${getLineRecord(form.lineId).typeName || '未分类'}）`,
      occurTime: form.occurTime || timestamp(),
      reporterId: 1,
      reporterName: '当前操作员',
      status: 'pending_repair',
    }
    faultRecords.unshift(record)
    getLineRecord(form.lineId).status = 'fault'
    const lineStatus = lineStatuses.find((item) => item.lineId === form.lineId)
    if (lineStatus) {
      lineStatus.currentOrderId = undefined
      lineStatus.efficiency = 0
      lineStatus.status = 'fault'
    }
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
    record.repairerName = undefined
    if (form.repairerId) {
      record.repairerName = `维修员 ${form.repairerId}`
    }
    record.processingNote = form.processingNote?.trim() || undefined
    record.recoverTime = undefined
    if (form.status === 'recovered') {
      record.recoverTime = form.recoverTime
      getLineRecord(record.lineId).status = 'idle'
      const lineStatus = lineStatuses.find((item) => item.lineId === record.lineId)
      if (lineStatus) {
        lineStatus.currentOrderId = undefined
        lineStatus.efficiency = 0
        lineStatus.status = 'idle'
      }
    } else if (form.status === 'repairing') {
      getLineRecord(record.lineId).status = 'fault'
      const lineStatus = lineStatuses.find((item) => item.lineId === record.lineId)
      if (lineStatus) {
        lineStatus.status = 'fault'
      }
    }
    return structuredClone(record)
  })
}

export interface ProductionMockState {
  calendars: ProductionCalendarItem[]
  capacityBalances?: CapacityBalanceItem[]
  capacityConfigs: CapacityConfigItem[]
  capacityDetections?: CapacityDetectionItem[]
  externalOrders: ExternalOrderItem[]
  faultRecords: FaultRecordItem[]
  lineStatuses?: ProductionLineStatusItem[]
  lineTypes: LineTypeItem[]
  lines: ProductionLineItem[]
  productionOrders: ProductionOrderItem[]
  orderSchedules?: ProductionScheduleItem[]
  orderStages?: Record<number, ProductionStageItem[]>
  typeNames: Record<number, string>
}

export function snapshotProductionMock(): ProductionMockState {
  return structuredClone({
    calendars,
    capacityBalances,
    capacityConfigs,
    capacityDetections,
    externalOrders,
    faultRecords,
    lineStatuses,
    lineTypes,
    lines,
    orderSchedules,
    orderStages,
    productionOrders,
    typeNames,
  })
}

export function restoreProductionMock(state: ProductionMockState) {
  Object.keys(typeNames).forEach((key) => delete typeNames[Number(key)])
  Object.assign(typeNames, structuredClone(state.typeNames))
  productionOrders.splice(0, productionOrders.length, ...structuredClone(state.productionOrders))
  if (Array.isArray(state.orderSchedules)) {
    orderSchedules.splice(0, orderSchedules.length, ...structuredClone(state.orderSchedules))
  }
  if (state.orderStages) {
    Object.keys(orderStages).forEach((key) => delete orderStages[Number(key)])
    Object.assign(orderStages, structuredClone(state.orderStages))
  }
  productionOrders.forEach((order) => {
    orderStages[order.orderId] = orderStages[order.orderId] ?? createOrderStages(order)
  })
  lineTypes.splice(0, lineTypes.length, ...structuredClone(state.lineTypes))
  lines.splice(0, lines.length, ...structuredClone(state.lines))
  capacityConfigs.splice(0, capacityConfigs.length, ...structuredClone(state.capacityConfigs))
  calendars.splice(0, calendars.length, ...structuredClone(state.calendars))
  externalOrders.splice(0, externalOrders.length, ...structuredClone(state.externalOrders))
  const restoredFaultRecords = structuredClone(state.faultRecords)
  restoredFaultRecords.forEach((record) => {
    record.faultLevel = record.faultLevel ?? 'major'
    record.lineName = record.lineName ?? `生产线 ${record.lineId}`
  })
  faultRecords.splice(0, faultRecords.length, ...restoredFaultRecords)
  capacityDetections.splice(
    0,
    capacityDetections.length,
    ...structuredClone(state.capacityDetections ?? []),
  )
  capacityBalances.splice(
    0,
    capacityBalances.length,
    ...structuredClone(state.capacityBalances ?? []),
  )
  const restoredLineStatuses = structuredClone(state.lineStatuses ?? lineStatuses)
  lineStatuses.splice(0, lineStatuses.length, ...restoredLineStatuses)
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

function listOrdersWithSchedules(query: ProductionOrderQuery) {
  return delay(() => {
    const filtered = productionOrders.filter(
      (order) =>
        (!query.materialId || order.materialId === query.materialId) &&
        (!query.status || order.status === query.status) &&
        includesDate(order.planEnd, query.planEndStart, query.planEndEnd),
    )
    return paginate(filtered.map(toOrderWithSchedule), query.page, query.pageSize)
  })
}

function listFaults(query: FaultRecordQuery) {
  return delay(() =>
    paginate(
      faultRecords.filter(
        (record) =>
          (!query.lineId || record.lineId === query.lineId) &&
          (!query.status || record.status === query.status) &&
          (!query.faultType || record.faultType.includes(query.faultType.trim())) &&
          includesDate(record.occurTime, query.occurStart, query.occurEnd),
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
  addExternalOrder,
  approveOrder,
  cancelOrder,
  convertExternalOrder,
  createLine,
  createOrder,
  deleteCalendar,
  estimateCapacity,
  finishOrder,
  getFault(faultId: number) {
    return delay(() => {
      const record = faultRecords.find((item) => item.faultId === faultId)
      if (!record) {
        throw new Error('未找到故障记录')
      }
      return structuredClone(record)
    })
  },
  getOrder(orderId: number) {
    return delay(() => {
      const order = productionOrders.find((item) => item.orderId === orderId)
      if (!order) {
        throw new Error('未找到生产订单')
      }
      return toOrderWithSchedule(order)
    })
  },
  getOrderStages(orderId: number) {
    return delay(() => structuredClone(getOrderStagesRecord(getOrderRecord(orderId))))
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
  listFaults,
  listLineTypes: listTypes,
  listLines,
  listOrders: listOrdersWithSchedules,
  reportFault,
  reportOrderProgress,
  reviewExternalOrder,
  runCapacityDetection,
  saveCalendar,
  saveCapacityBalance,
  saveCapacityConfig,
  saveLineType,
  saveOrderSchedule,
  startOrder,
  updateFault,
  updateLine,
  updateLineStatus,
  updateOrder,
}

export type ProductionMockWrite =
  | 'addExternalOrder'
  | 'approveOrder'
  | 'cancelOrder'
  | 'createLine'
  | 'createOrder'
  | 'convertExternalOrder'
  | 'deleteCalendar'
  | 'finishOrder'
  | 'reportFault'
  | 'reportOrderProgress'
  | 'reviewExternalOrder'
  | 'runCapacityDetection'
  | 'saveCalendar'
  | 'saveCapacityBalance'
  | 'saveCapacityConfig'
  | 'saveLineType'
  | 'saveOrderSchedule'
  | 'startOrder'
  | 'updateFault'
  | 'updateLine'
  | 'updateLineStatus'
  | 'updateOrder'
