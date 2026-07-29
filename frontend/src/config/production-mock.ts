import type {
  CapacityConfigItem,
  CapacityConfigQuery,
  ExternalOrderItem,
  ExternalOrderQuery,
  LineTypeItem,
  LineTypeQuery,
  ProductionCalendarItem,
  ProductionCalendarQuery,
  ProductionLineItem,
  ProductionLineQuery,
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
