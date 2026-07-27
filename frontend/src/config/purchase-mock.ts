import type {
  PurchaseDraftItem,
  PurchaseDraftResult,
  PurchaseOrderFormData,
  PurchaseOrderItem,
  PurchaseOrderQuery,
  PurchaseReceiptFormData,
  PurchaseReceiptItem,
  PurchaseReceiptQuery,
  PurchaseReminderItem,
  PurchaseReminderQuery,
} from '@/types/purchase'
import type { PageResult } from '@/services/pagination'

const materialNames: Record<number, string> = {
  1001: '铝合金型材 6061',
  1002: '控制板组件 C01',
  1003: '屏蔽线缆 0.25mm',
  1004: '内六角螺钉 M4',
}

const suppliers = {
  41: {
    contactPerson: '陈敏',
    contactPhone: '138****3201',
    supplierId: 41,
    supplierName: '华东精材供应链',
  },
  42: {
    contactPerson: '周宇',
    contactPhone: '139****6840',
    supplierId: 42,
    supplierName: '启航电子组件',
  },
}

let orders: PurchaseOrderItem[] = [
  {
    buyerId: 1,
    details: [
      {
        itemId: 1101,
        lineAmount: 9600,
        materialId: 1001,
        materialName: materialNames[1001],
        orderId: 10_001,
        quantity: 120,
        receiveProgress: 0.5,
        receivedQty: 60,
        unitPrice: 80,
      },
    ],
    expectedDate: '2026-07-25',
    isOverdue: true,
    orderDate: '2026-07-12',
    orderId: 10_001,
    overdueDays: 2,
    receiveProgress: 0.5,
    status: 'partial_received',
    supplier: suppliers[41],
    totalAmount: 9600,
  },
  {
    buyerId: 1,
    details: [
      {
        itemId: 1102,
        lineAmount: 18_000,
        materialId: 1002,
        materialName: materialNames[1002],
        orderId: 10_002,
        quantity: 60,
        receiveProgress: 0,
        receivedQty: 0,
        unitPrice: 300,
      },
    ],
    expectedDate: '2026-07-26',
    isOverdue: true,
    orderDate: '2026-07-15',
    orderId: 10_002,
    overdueDays: 1,
    receiveProgress: 0,
    status: 'submitted',
    supplier: suppliers[42],
    totalAmount: 18_000,
  },
  {
    buyerId: 1,
    details: [
      {
        itemId: 1103,
        lineAmount: 3600,
        materialId: 1003,
        materialName: materialNames[1003],
        orderId: 10_003,
        quantity: 200,
        receiveProgress: 0,
        receivedQty: 0,
        unitPrice: 18,
      },
    ],
    expectedDate: '2026-08-08',
    isOverdue: false,
    orderDate: '2026-07-26',
    orderId: 10_003,
    overdueDays: 0,
    receiveProgress: 0,
    status: 'draft',
    supplier: suppliers[41],
    totalAmount: 3600,
  },
  {
    actualDate: '2026-07-22',
    buyerId: 2,
    details: [
      {
        itemId: 1104,
        lineAmount: 1800,
        materialId: 1004,
        materialName: materialNames[1004],
        orderId: 10_004,
        quantity: 600,
        receiveProgress: 1,
        receivedQty: 600,
        unitPrice: 3,
      },
    ],
    expectedDate: '2026-07-24',
    isOverdue: false,
    orderDate: '2026-07-10',
    orderId: 10_004,
    overdueDays: 0,
    receiveProgress: 1,
    status: 'completed',
    supplier: suppliers[42],
    totalAmount: 1800,
  },
]

let receipts: PurchaseReceiptItem[] = [
  {
    materialId: 1001,
    materialName: materialNames[1001],
    orderId: 10_001,
    quantity: 60,
    receiveDate: '2026-07-24',
    receiveId: 1201,
  },
  {
    materialId: 1004,
    materialName: materialNames[1004],
    orderId: 10_004,
    quantity: 600,
    receiveDate: '2026-07-22',
    receiveId: 1202,
  },
]

let reminders: PurchaseReminderItem[] = [
  {
    expectedDate: '2026-07-25',
    orderId: 10_001,
    overdueDays: 2,
    remark: '供应商承诺今日发出剩余批次',
    remindTime: '2026-07-26T08:00:00',
    reminderId: 1301,
    status: 'urged',
  },
  {
    expectedDate: '2026-07-26',
    orderId: 10_002,
    overdueDays: 1,
    remindTime: '2026-07-27T08:00:00',
    reminderId: 1302,
    status: 'pending_urge',
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

function recalculateOrder(order: PurchaseOrderItem) {
  const ordered = order.details.reduce((sum, item) => sum + item.quantity, 0)
  const received = order.details.reduce((sum, item) => sum + item.receivedQty, 0)
  order.receiveProgress = 0
  if (ordered > 0) {
    order.receiveProgress = received / ordered
  }
  if (order.receiveProgress >= 1) {
    order.status = 'completed'
    order.actualDate = new Date().toISOString().slice(0, 10)
    order.isOverdue = false
    order.overdueDays = 0
  } else if (received > 0) {
    order.status = 'partial_received'
  }
}

function createOrder(form: PurchaseOrderFormData) {
  const newOrderId =
    Math.max(10_000, ...orders.map(({ orderId: existingOrderId }) => existingOrderId)) + 1
  const details = form.details.map((item, index) => ({
    ...item,
    itemId: newOrderId * 10 + index + 1,
    lineAmount: item.quantity * item.unitPrice,
    materialName: materialNames[item.materialId] ?? `物料 #${item.materialId}`,
    orderId: newOrderId,
    receiveProgress: 0,
    receivedQty: 0,
  }))
  const order: PurchaseOrderItem = {
    buyerId: form.buyerId,
    details,
    expectedDate: form.expectedDate,
    isOverdue: false,
    orderDate: new Date().toISOString().slice(0, 10),
    orderId: newOrderId,
    overdueDays: 0,
    receiveProgress: 0,
    status: 'draft',
    supplier: suppliers[form.supplierId as keyof typeof suppliers] ?? {
      supplierId: form.supplierId,
      supplierName: `供应商 #${form.supplierId}`,
    },
    totalAmount: details.reduce((sum, item) => sum + item.lineAmount, 0),
  }
  orders = [order, ...orders]
  return order
}

function getDefaultSupplierId(item: PurchaseDraftItem) {
  if (item.supplierId !== undefined) {
    return item.supplierId
  }
  if (item.materialId === 1002) {
    return 42
  }
  if (materialNames[item.materialId]) {
    return 41
  }
  return undefined
}

function getDefaultUnitPrice(materialId: number) {
  if (materialId === 1002) {
    return 300
  }
  return 20
}

export const purchaseMock = {
  addReceipt(form: PurchaseReceiptFormData) {
    return delay(() => {
      const order = orders.find(({ orderId }) => orderId === form.orderId)
      const line = order?.details.find(({ materialId }) => materialId === form.materialId)
      if (!order || !line || !['partial_received', 'submitted'].includes(order.status)) {
        throw new Error('采购订单不存在或当前状态不可收货')
      }
      if (line.receivedQty + form.quantity > line.quantity) {
        throw new Error('本次收货数量超过订单剩余未收数量')
      }
      line.receivedQty += form.quantity
      line.receiveProgress = line.receivedQty / line.quantity
      recalculateOrder(order)
      const item: PurchaseReceiptItem = {
        ...form,
        materialName: line.materialName,
        receiveId: Math.max(1200, ...receipts.map(({ receiveId }) => receiveId)) + 1,
      }
      receipts = [item, ...receipts]
      return { ...item }
    })
  },

  cancelOrder(orderId: number, _operatorId: number) {
    return delay(() => {
      const order = orders.find((item) => item.orderId === orderId)
      if (!order || !['draft', 'submitted'].includes(order.status)) {
        throw new Error('该采购订单当前不可取消')
      }
      order.status = 'cancelled'
      order.isOverdue = false
      return { ...order }
    })
  },

  createDrafts(
    items: PurchaseDraftItem[],
    buyerId: number,
    expectedDate: string,
  ): Promise<PurchaseDraftResult> {
    return delay(() => {
      const grouped = new Map<number, PurchaseDraftItem[]>()
      const unassignedItems: { materialId: number; purchaseQty: number }[] = []
      for (const item of items) {
        const supplierId = getDefaultSupplierId(item)
        if (supplierId === undefined) {
          unassignedItems.push({ materialId: item.materialId, purchaseQty: item.purchaseQty })
        } else {
          grouped.set(supplierId, [...(grouped.get(supplierId) ?? []), item])
        }
      }
      const created = [...grouped.entries()].map(([supplierId, supplierItems]) =>
        createOrder({
          buyerId,
          details: supplierItems.map((item) => ({
            materialId: item.materialId,
            quantity: item.purchaseQty,
            unitPrice: getDefaultUnitPrice(item.materialId),
          })),
          expectedDate,
          supplierId,
        }),
      )
      return {
        createdCount: created.length,
        items: created,
        unassignedItems,
      }
    })
  },

  createOrder(form: PurchaseOrderFormData) {
    return delay(() => ({ ...createOrder(form) }))
  },

  generateReminders(orderId?: number) {
    return delay(() => {
      const candidates = orders.filter(
        (order) =>
          order.isOverdue &&
          (!orderId || order.orderId === orderId) &&
          !reminders.some(
            (reminder) => reminder.orderId === order.orderId && reminder.status === 'pending_urge',
          ),
      )
      const created = candidates.map((order, index) => ({
        expectedDate: order.expectedDate,
        orderId: order.orderId,
        overdueDays: order.overdueDays,
        remindTime: new Date().toISOString(),
        reminderId: Math.max(1300, ...reminders.map(({ reminderId }) => reminderId)) + index + 1,
        status: 'pending_urge' as const,
      }))
      reminders = [...created, ...reminders]
      return { generatedCount: created.length, items: created }
    })
  },

  getOrder(orderId: number) {
    return delay(() => {
      const order = orders.find((item) => item.orderId === orderId)
      if (!order) {
        throw new Error('未找到该采购订单')
      }
      return structuredClone(order)
    })
  },

  handleReminder(reminderId: number, status: 'received' | 'urged', remark?: string) {
    return delay(() => {
      const reminder = reminders.find((item) => item.reminderId === reminderId)
      if (!reminder || reminder.status === 'received') {
        throw new Error('该逾期提醒不存在或已完成')
      }
      if (status === 'urged' && reminder.status !== 'pending_urge') {
        throw new Error('仅待催交提醒可更新为已催交')
      }
      Object.assign(reminder, { remark: remark?.trim() || undefined, status })
      return { ...reminder }
    })
  },

  listOrders(query: PurchaseOrderQuery) {
    return delay(() =>
      paginate(
        orders.filter(
          (item) =>
            (!query.supplierId || item.supplier.supplierId === query.supplierId) &&
            (!query.materialId ||
              item.details.some(({ materialId }) => materialId === query.materialId)) &&
            (!query.status || item.status === query.status) &&
            (!query.buyerId || item.buyerId === query.buyerId) &&
            (!query.orderDateStart || item.orderDate >= query.orderDateStart) &&
            (!query.orderDateEnd || item.orderDate <= query.orderDateEnd) &&
            (!query.expectedDateStart || item.expectedDate >= query.expectedDateStart) &&
            (!query.expectedDateEnd || item.expectedDate <= query.expectedDateEnd),
        ),
        query.page,
        query.pageSize,
      ),
    )
  },

  listReceipts(query: PurchaseReceiptQuery) {
    return delay(() =>
      paginate(
        receipts.filter(
          (item) =>
            (!query.orderId || item.orderId === query.orderId) &&
            (!query.materialId || item.materialId === query.materialId),
        ),
        query.page,
        query.pageSize,
      ),
    )
  },

  listReminders(query: PurchaseReminderQuery) {
    return delay(() =>
      paginate(
        reminders.filter(
          (item) =>
            (!query.orderId || item.orderId === query.orderId) &&
            (!query.status || item.status === query.status),
        ),
        query.page,
        query.pageSize,
      ),
    )
  },

  submitOrder(orderId: number, _operatorId: number) {
    return delay(() => {
      const order = orders.find((item) => item.orderId === orderId)
      if (!order || order.status !== 'draft') {
        throw new Error('仅草稿采购订单可提交')
      }
      order.status = 'submitted'
      return { ...order }
    })
  },
}
