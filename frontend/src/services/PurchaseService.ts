import {
  type ApiEnvelope,
  ApiRequestError,
  type PageResult,
  getPageItems,
  getPageMetadata,
  mapPageResult,
  nullableText,
  optionalText,
  unwrap,
} from '@/services/pagination'
import type {
  Material,
  PurchaseBuyerBrief,
  PurchaseDraftFromShortageResponseAllOfData,
  PurchaseOrder,
  PurchaseOverdueReminder,
  PurchaseReceipt,
  SupplierDetail,
} from '@/api'
import type {
  PurchaseDraftItem,
  PurchaseDraftResult,
  PurchaseOrderFormData,
  PurchaseOrderItem,
  PurchaseOrderQuery,
  PurchaseOverviewSummary,
  PurchaseReceiptFormData,
  PurchaseReceiptItem,
  PurchaseReceiptQuery,
  PurchaseReferenceData,
  PurchaseReminderItem,
  PurchaseReminderQuery,
  PurchaseSupplierQuery,
  SupplierInfo,
} from '@/types/purchase'
import { materialBomApi, purchaseApi } from '@/api/client'
import { cleanQuery } from '@/services/request'
import { pinia } from '@/stores/pinia'
import { useAuthStore } from '@/stores/auth'

export type { PageResult }

function toOrder(item: PurchaseOrder): PurchaseOrderItem {
  return {
    actualDate: optionalText(item.actual_date),
    buyerId: item.buyer_id,
    details: item.details.map((detail) => ({
      itemId: detail.item_id,
      lineAmount: detail.line_amount,
      materialId: detail.material_id,
      materialName: optionalText(detail.material_name),
      orderId: detail.order_id,
      quantity: detail.quantity,
      receiveProgress: detail.receive_progress ?? 0,
      receivedQty: detail.received_qty,
      unitPrice: detail.unit_price,
    })),
    expectedDate: item.expected_date,
    isOverdue: item.is_overdue,
    orderDate: item.order_date,
    orderId: item.order_id,
    overdueDays: item.overdue_days ?? 0,
    receiveProgress: item.receive_progress,
    status: item.status,
    supplier: {
      contactPerson: optionalText(item.supplier.contact_person),
      contactPhone: optionalText(item.supplier.contact_phone),
      supplierId: item.supplier.supplier_id,
      supplierName: item.supplier.supplier_name,
    },
    totalAmount: item.total_amount,
  }
}

function toReceipt(item: PurchaseReceipt): PurchaseReceiptItem {
  return {
    materialId: item.material_id,
    materialName: optionalText(item.material_name),
    orderId: item.order_id,
    quantity: item.quantity,
    receiveDate: item.receive_date,
    receiveId: item.receive_id,
  }
}

function toReminder(item: PurchaseOverdueReminder): PurchaseReminderItem {
  return {
    expectedDate: item.expected_date,
    orderId: item.order_id,
    overdueDays: item.overdue_days,
    remark: optionalText(item.remark),
    remindTime: item.remind_time,
    reminderId: item.reminder_id,
    status: item.status,
  }
}

function toSupplier(item: SupplierDetail): SupplierInfo {
  return {
    contactPerson: optionalText(item.contact_person),
    contactPhone: optionalText(item.contact_phone),
    supplierId: item.supplier_id,
    supplierName: item.supplier_name,
  }
}

function requireData<TData>(response: ApiEnvelope<TData>) {
  const data = unwrap(response)
  if (response.code !== 200 || data === undefined || data === null) {
    throw new Error('接口响应数据无效')
  }
  return data
}

function matchesOrderQuery(order: PurchaseOrderItem, query: PurchaseOrderQuery) {
  return (
    (query.buyerId === undefined || order.buyerId === query.buyerId) &&
    (query.supplierId === undefined || order.supplier.supplierId === query.supplierId) &&
    (query.materialId === undefined ||
      order.details.some((item) => item.materialId === query.materialId)) &&
    (!query.status || order.status === query.status) &&
    (!query.orderDateStart || order.orderDate >= query.orderDateStart) &&
    (!query.orderDateEnd || order.orderDate <= query.orderDateEnd) &&
    (!query.expectedDateStart || order.expectedDate >= query.expectedDateStart) &&
    (!query.expectedDateEnd || order.expectedDate <= query.expectedDateEnd)
  )
}

function validateOrderForm(form: PurchaseOrderFormData) {
  if (!Number.isInteger(form.buyerId) || form.buyerId <= 0) {
    throw new Error('请选择有效采购员')
  }
  if (!Number.isInteger(form.supplierId) || form.supplierId <= 0) {
    throw new Error('请选择有效供应商')
  }
  const dateParts = /^(\d{4})-(\d{2})-(\d{2})$/.exec(form.expectedDate)
  if (!dateParts) {
    throw new Error('预计到货日期格式无效')
  }
  const expectedDate = new Date(
    Number(dateParts[1]),
    Number(dateParts[2]) - 1,
    Number(dateParts[3]),
  )
  if (
    expectedDate.getFullYear() !== Number(dateParts[1]) ||
    expectedDate.getMonth() !== Number(dateParts[2]) - 1 ||
    expectedDate.getDate() !== Number(dateParts[3])
  ) {
    throw new Error('预计到货日期格式无效')
  }
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  if (expectedDate < today) {
    throw new Error('预计到货日期不能早于当前日期')
  }
  if (!form.details.length) {
    throw new Error('采购订单至少需要一条明细')
  }
  const materialIds = new Set<number>()
  for (const item of form.details) {
    if (!Number.isInteger(item.materialId) || item.materialId <= 0) {
      throw new Error('请选择有效采购物料')
    }
    if (!Number.isFinite(item.quantity) || item.quantity <= 0) {
      throw new Error('采购数量必须大于 0')
    }
    if (!Number.isFinite(item.unitPrice) || item.unitPrice < 0) {
      throw new Error('含税单价不能小于 0')
    }
    if (materialIds.has(item.materialId)) {
      throw new Error('同一物料不能重复添加')
    }
    materialIds.add(item.materialId)
  }
}

async function loadAllRecords<TRecord>(
  request: (page: number, pageSize: number) => Promise<{ data: unknown }>,
) {
  const pageSize = 100
  const firstResponse = await request(1, pageSize)
  const firstPayload = requireData(firstResponse.data as ApiEnvelope<unknown>)
  const firstItems = getPageItems<TRecord>(firstPayload)
  const metadata = getPageMetadata(firstPayload, { page: 1, pageSize, total: firstItems.length })
  const remainingPageCount = Math.ceil(metadata.total / metadata.pageSize) - 1
  if (remainingPageCount <= 0) {
    return firstItems
  }
  const remainingResponses = await Promise.all(
    Array.from({ length: remainingPageCount }, (unusedValue, index) => {
      void unusedValue
      return request(index + 2, metadata.pageSize)
    }),
  )
  return [
    ...firstItems,
    ...remainingResponses.flatMap((response) => {
      const payload = requireData(response.data as ApiEnvelope<unknown>)
      return getPageItems<TRecord>(payload)
    }),
  ]
}

export const purchaseService = {
  async addReceipt(form: PurchaseReceiptFormData) {
    const response = await purchaseApi.addPurchaseReceipt({
      purchaseReceiptCreateRequest: {
        material_id: form.materialId,
        order_id: form.orderId,
        quantity: form.quantity,
        receive_date: form.receiveDate,
      },
    })
    const data = requireData(response.data as ApiEnvelope<PurchaseReceipt>)
    return toReceipt(data)
  },

  async cancelOrder(orderId: number, operatorId: number) {
    const response = await purchaseApi.cancelPurchaseOrder({
      purchaseOrderActionRequest: { operator_id: operatorId, order_id: orderId },
    })
    const data = requireData(response.data as ApiEnvelope<PurchaseOrder>)
    return toOrder(data)
  },

  async createDrafts(
    items: PurchaseDraftItem[],
    buyerId: number,
    expectedDate: string,
  ): Promise<PurchaseDraftResult> {
    const response = await purchaseApi.createPurchaseOrderDraftFromShortage({
      purchaseDraftFromShortageRequest: {
        buyer_id: buyerId,
        expected_date: expectedDate,
        items: items.map((item) => ({
          material_id: item.materialId,
          purchase_qty: item.purchaseQty,
          supplier_id: item.supplierId,
        })),
      },
    })
    const data = requireData(
      response.data as ApiEnvelope<PurchaseDraftFromShortageResponseAllOfData>,
    )
    const unassignedItems: { materialId: number; purchaseQty: number }[] = []
    for (const item of data.unassigned_items ?? []) {
      if (item.material_id !== undefined && item.purchase_qty !== undefined) {
        unassignedItems.push({ materialId: item.material_id, purchaseQty: item.purchase_qty })
      }
    }
    return {
      createdCount: data.created_count ?? 0,
      items: (data.records ?? []).map(toOrder),
      unassignedItems,
    }
  },

  async createOrder(form: PurchaseOrderFormData) {
    validateOrderForm(form)
    const response = await purchaseApi.addPurchaseOrder({
      purchaseOrderCreateRequest: {
        buyer_id: form.buyerId,
        details: form.details.map((item) => ({
          material_id: item.materialId,
          quantity: item.quantity,
          unit_price: item.unitPrice,
        })),
        expected_date: form.expectedDate,
        supplier_id: form.supplierId,
      },
    })
    const data = requireData(response.data as ApiEnvelope<PurchaseOrder>)
    return toOrder(data)
  },

  async generateReminders(orderId?: number) {
    let request: { order_id: number } | undefined = undefined
    if (orderId !== undefined) {
      request = { order_id: orderId }
    }
    const response = await purchaseApi.generatePurchaseOverdueReminder({
      purchaseOverdueReminderGenerateRequest: request,
    })
    const data = requireData(
      response.data as ApiEnvelope<
        | {
            generated_count?: number
            records?: PurchaseOverdueReminder[]
          }
        | undefined
      >,
    )
    return {
      generatedCount: data.generated_count ?? 0,
      items: (data.records ?? []).map(toReminder),
    }
  },

  async getOrder(orderId: number) {
    const response = await purchaseApi.getPurchaseOrder({ orderId })
    const data = requireData(response.data as ApiEnvelope<PurchaseOrder>)
    return toOrder(data)
  },

  async getOverview(): Promise<PurchaseOverviewSummary> {
    const today = new Date().toISOString().slice(0, 10)
    const [all, firstOverduePage, submitted, partial, reminders] = await Promise.all([
      this.listOrders({ page: 1, pageSize: 1 }),
      this.listOrders({
        expectedDateEnd: today,
        page: 1,
        pageSize: 100,
      }),
      this.listOrders({ page: 1, pageSize: 1, status: 'submitted' }),
      this.listOrders({ page: 1, pageSize: 1, status: 'partial_received' }),
      this.listReminders({ page: 1, pageSize: 1, status: 'pending_urge' }),
    ])
    const remainingPageCount = Math.ceil(firstOverduePage.total / firstOverduePage.pageSize) - 1
    let overdueItems = [...firstOverduePage.items]
    if (remainingPageCount > 0) {
      const remainingPages = await Promise.all(
        Array.from({ length: remainingPageCount }, (unusedValue, index) => {
          void unusedValue
          return this.listOrders({
            expectedDateEnd: today,
            page: index + 2,
            pageSize: firstOverduePage.pageSize,
          })
        }),
      )
      overdueItems = [...overdueItems, ...remainingPages.flatMap(({ items }) => items)]
    }
    return {
      overdueOrderCount: overdueItems.filter((item) => item.isOverdue).length,
      pendingReminderCount: reminders.total,
      receivingOrderCount: submitted.total + partial.total,
      totalOrderCount: all.total,
    }
  },

  async getReferenceData(): Promise<PurchaseReferenceData> {
    const auth = useAuthStore(pinia)
    const canReadMaterials = auth.roles.some((role) =>
      ['系统管理员', '生产管理员', '采购员'].includes(role),
    )
    let materialsPromise = Promise.resolve<Material[]>([])
    if (canReadMaterials) {
      materialsPromise = loadAllRecords<Material>((page, pageSize) =>
        materialBomApi.listMaterialData({ page, pageSize }),
      )
    }
    const buyersPromise = loadAllRecords<PurchaseBuyerBrief>((page, pageSize) =>
      purchaseApi.listPurchaseBuyerData({ page, pageSize }),
    )
    const suppliersPromise = loadAllRecords<SupplierDetail>((page, pageSize) =>
      purchaseApi.listSupplierData({ page, pageSize }),
    )
    const [materialItems, firstOrderPage, buyerItems, supplierItems] = await Promise.all([
      materialsPromise,
      this.listOrders({ page: 1, pageSize: 100 }),
      buyersPromise,
      suppliersPromise,
    ])
    const remainingPageCount = Math.ceil(firstOrderPage.total / firstOrderPage.pageSize) - 1
    let orders = [...firstOrderPage.items]
    if (remainingPageCount > 0) {
      const pages = await Promise.all(
        Array.from({ length: remainingPageCount }, (unusedValue, index) => {
          void unusedValue
          return this.listOrders({ page: index + 2, pageSize: firstOrderPage.pageSize })
        }),
      )
      orders = [...orders, ...pages.flatMap((page) => page.items)]
    }
    const materials: PurchaseReferenceData['materials'] = materialItems
      .filter(
        (item): item is Material & { material_id: number; material_name: string } =>
          item.material_id !== undefined && Boolean(item.material_name),
      )
      .map((item) => ({
        defaultSupplierId: item.default_supplier_id ?? undefined,
        materialId: item.material_id,
        materialName: item.material_name,
        unit: item.unit,
      }))
    if (!canReadMaterials) {
      const materialMap = new Map<number, PurchaseReferenceData['materials'][number]>()
      for (const order of orders) {
        for (const line of order.details) {
          materialMap.set(line.materialId, {
            materialId: line.materialId,
            materialName: line.materialName ?? `物料 #${line.materialId}`,
          })
        }
      }
      materials.push(...materialMap.values())
    }
    const suppliers = supplierItems.map(toSupplier)
    const buyers = buyerItems.map((buyer) => ({
      buyerId: buyer.buyer_id,
      buyerName: buyer.buyer_name,
    }))
    if (auth.currentUser?.id && !buyers.some((buyer) => buyer.buyerId === auth.currentUser?.id)) {
      buyers.unshift({
        buyerId: auth.currentUser.id,
        buyerName: auth.currentUser.name ?? `采购员 #${auth.currentUser.id}`,
      })
    }
    return { buyers, materials, orders, suppliers }
  },

  async handleReminder(reminderId: number, status: 'received' | 'urged', remark?: string) {
    const response = await purchaseApi.handlePurchaseOverdueReminder({
      purchaseOverdueReminderHandleRequest: {
        remark: nullableText(remark),
        reminder_id: reminderId,
        status,
      },
    })
    const data = requireData(response.data as ApiEnvelope<PurchaseOverdueReminder>)
    return toReminder(data)
  },

  async listOrders(query: PurchaseOrderQuery) {
    const normalizedQuery = cleanQuery(query)
    if (normalizedQuery.orderId !== undefined) {
      let order: PurchaseOrderItem | undefined = undefined
      try {
        order = await this.getOrder(normalizedQuery.orderId)
      } catch (error) {
        if (!(error instanceof ApiRequestError) || error.status !== 404) {
          throw error
        }
      }
      const items: PurchaseOrderItem[] = []
      if (order && matchesOrderQuery(order, normalizedQuery)) {
        items.push(order)
      }
      const page = Math.max(1, normalizedQuery.page)
      const pageSize = Math.max(1, normalizedQuery.pageSize)
      return {
        items: items.slice((page - 1) * pageSize, page * pageSize),
        page,
        pageSize,
        total: items.length,
      }
    }
    const response = await purchaseApi.listPurchaseOrder({
      buyerId: normalizedQuery.buyerId,
      expectedDateEnd: normalizedQuery.expectedDateEnd,
      expectedDateStart: normalizedQuery.expectedDateStart,
      materialId: normalizedQuery.materialId,
      orderDateEnd: normalizedQuery.orderDateEnd,
      orderDateStart: normalizedQuery.orderDateStart,
      page: normalizedQuery.page,
      pageSize: normalizedQuery.pageSize,
      status: normalizedQuery.status,
      supplierId: normalizedQuery.supplierId,
    })
    const payload = requireData(response.data as ApiEnvelope<unknown>)
    return mapPageResult<PurchaseOrder, PurchaseOrderItem>(payload, normalizedQuery, toOrder)
  },

  async listReceipts(query: PurchaseReceiptQuery) {
    const normalizedQuery = cleanQuery(query)
    const response = await purchaseApi.listPurchaseReceipt({
      materialId: normalizedQuery.materialId,
      orderId: normalizedQuery.orderId,
      page: normalizedQuery.page,
      pageSize: normalizedQuery.pageSize,
    })
    const payload = requireData(response.data as ApiEnvelope<unknown>)
    return mapPageResult<PurchaseReceipt, PurchaseReceiptItem>(payload, normalizedQuery, toReceipt)
  },

  async listReminders(query: PurchaseReminderQuery) {
    const normalizedQuery = cleanQuery(query)
    const response = await purchaseApi.listPurchaseOverdueReminder({
      orderId: normalizedQuery.orderId,
      page: normalizedQuery.page,
      pageSize: normalizedQuery.pageSize,
      status: normalizedQuery.status,
    })
    const payload = requireData(response.data as ApiEnvelope<unknown>)
    return mapPageResult<PurchaseOverdueReminder, PurchaseReminderItem>(
      payload,
      normalizedQuery,
      toReminder,
    )
  },

  async listSuppliers(query: PurchaseSupplierQuery) {
    const normalizedQuery = cleanQuery(query)
    const response = await purchaseApi.listSupplierData({
      page: normalizedQuery.page,
      pageSize: normalizedQuery.pageSize,
      supplierId: normalizedQuery.supplierId,
      supplierName: normalizedQuery.supplierName,
    })
    const payload = requireData(response.data as ApiEnvelope<unknown>)
    return mapPageResult<SupplierDetail, SupplierInfo>(payload, normalizedQuery, toSupplier)
  },

  async submitOrder(orderId: number, operatorId: number) {
    const response = await purchaseApi.submitPurchaseOrder({
      purchaseOrderActionRequest: { operator_id: operatorId, order_id: orderId },
    })
    const data = requireData(response.data as ApiEnvelope<PurchaseOrder>)
    return toOrder(data)
  },
}
