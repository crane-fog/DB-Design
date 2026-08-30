import {
  type ApiEnvelope,
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
  MaterialDetail,
  PurchaseDraftFromShortageResponseAllOfData,
  PurchaseOrder,
  PurchaseOverdueReminder,
  PurchaseReceipt,
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
} from '@/types/purchase'
import { materialBomApi, purchaseApi } from '@/api/client'
import { cleanQuery } from '@/services/request'
import { isMockEnabled } from '@/config/mock'
import { purchaseRepository as purchaseMock } from '@/mock/repositories'

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

function mapOptional<TSource, TResult>(
  value: TSource | undefined,
  mapper: (item: TSource) => TResult,
) {
  if (value === undefined) {
    return undefined
  }
  return mapper(value)
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

async function loadAllMaterials() {
  const pageSize = 100
  const firstResponse = await materialBomApi.listMaterialData({ page: 1, pageSize })
  const firstPayload = unwrap(firstResponse.data as ApiEnvelope<unknown>)
  const firstItems = getPageItems<Material>(firstPayload)
  const metadata = getPageMetadata(firstPayload, { page: 1, pageSize, total: firstItems.length })
  const remainingPageCount = Math.ceil(metadata.total / metadata.pageSize) - 1
  if (remainingPageCount <= 0) {
    return firstItems
  }
  const remainingResponses = await Promise.all(
    Array.from({ length: remainingPageCount }, (unusedValue, index) => {
      void unusedValue
      return materialBomApi.listMaterialData({
        page: index + 2,
        pageSize: metadata.pageSize,
      })
    }),
  )
  return [
    ...firstItems,
    ...remainingResponses.flatMap((response) => {
      const payload = unwrap(response.data as ApiEnvelope<unknown>)
      return getPageItems<Material>(payload)
    }),
  ]
}

export const purchaseService = {
  async addReceipt(form: PurchaseReceiptFormData) {
    if (isMockEnabled()) {
      return purchaseMock.addReceipt(form)
    }
    const response = await purchaseApi.addPurchaseReceipt({
      purchaseReceiptCreateRequest: {
        material_id: form.materialId,
        order_id: form.orderId,
        quantity: form.quantity,
        receive_date: form.receiveDate,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<PurchaseReceipt | undefined>)
    return mapOptional(data, toReceipt)
  },

  canUpdateDraft() {
    return isMockEnabled()
  },

  async cancelOrder(orderId: number, operatorId: number) {
    if (isMockEnabled()) {
      return purchaseMock.cancelOrder(orderId, operatorId)
    }
    const response = await purchaseApi.cancelPurchaseOrder({
      purchaseOrderActionRequest: { operator_id: operatorId, order_id: orderId },
    })
    const data = unwrap(response.data as ApiEnvelope<PurchaseOrder | undefined>)
    return mapOptional(data, toOrder)
  },

  async createDrafts(
    items: PurchaseDraftItem[],
    buyerId: number,
    expectedDate: string,
  ): Promise<PurchaseDraftResult> {
    if (isMockEnabled()) {
      return purchaseMock.createDrafts(items, buyerId, expectedDate)
    }
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
    const data = unwrap(
      response.data as ApiEnvelope<PurchaseDraftFromShortageResponseAllOfData | undefined>,
    )
    const unassignedItems: { materialId: number; purchaseQty: number }[] = []
    for (const item of data?.unassigned_items ?? []) {
      if (item.material_id !== undefined && item.purchase_qty !== undefined) {
        unassignedItems.push({ materialId: item.material_id, purchaseQty: item.purchase_qty })
      }
    }
    return {
      createdCount: data?.created_count ?? 0,
      items: (data?.records ?? []).map(toOrder),
      unassignedItems,
    }
  },

  async createOrder(form: PurchaseOrderFormData) {
    validateOrderForm(form)
    if (isMockEnabled()) {
      return purchaseMock.createOrder(form)
    }
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
    const data = unwrap(response.data as ApiEnvelope<PurchaseOrder | undefined>)
    return mapOptional(data, toOrder)
  },

  async generateReminders(orderId?: number) {
    if (isMockEnabled()) {
      return purchaseMock.generateReminders(orderId)
    }
    let request: { order_id: number } | undefined = undefined
    if (orderId !== undefined) {
      request = { order_id: orderId }
    }
    const response = await purchaseApi.generatePurchaseOverdueReminder({
      purchaseOverdueReminderGenerateRequest: request,
    })
    const data = unwrap(
      response.data as ApiEnvelope<
        | {
            generated_count?: number
            records?: PurchaseOverdueReminder[]
          }
        | undefined
      >,
    )
    return {
      generatedCount: data?.generated_count ?? 0,
      items: (data?.records ?? []).map(toReminder),
    }
  },

  async getOrder(orderId: number) {
    if (isMockEnabled()) {
      return purchaseMock.getOrder(orderId)
    }
    const response = await purchaseApi.getPurchaseOrder({ orderId })
    const data = unwrap(response.data as ApiEnvelope<PurchaseOrder | undefined>)
    return mapOptional(data, toOrder)
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
    if (isMockEnabled()) {
      return purchaseMock.getReferenceData()
    }
    const [materialItems, firstOrderPage] = await Promise.all([
      loadAllMaterials(),
      this.listOrders({ page: 1, pageSize: 100 }),
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
    const materials = materialItems
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
    const supplierMap = new Map(
      orders.map((order) => [order.supplier.supplierId, order.supplier] as const),
    )
    const representativeMaterialBySupplier = new Map<number, number>()
    for (const material of materials) {
      if (material.defaultSupplierId && !supplierMap.has(material.defaultSupplierId)) {
        representativeMaterialBySupplier.set(material.defaultSupplierId, material.materialId)
      }
    }
    const supplierDetails = await Promise.all(
      [...representativeMaterialBySupplier.entries()].map(async ([supplierId, materialId]) => {
        const response = await materialBomApi.getMaterialData({ materialId })
        const detail = unwrap(response.data as ApiEnvelope<MaterialDetail | undefined>)
        return {
          supplierId,
          supplierName: optionalText(detail?.supplier_name) ?? `供应商 #${supplierId}`,
        }
      }),
    )
    for (const supplier of supplierDetails) {
      supplierMap.set(supplier.supplierId, supplier)
    }
    const suppliers = [...supplierMap.values()]
    const buyers = [...new Set(orders.map((order) => order.buyerId))].map((buyerId) => ({
      buyerId,
      buyerName: `采购员 #${buyerId}`,
    }))
    return { buyers, materials, orders, suppliers }
  },

  async handleReminder(reminderId: number, status: 'received' | 'urged', remark?: string) {
    if (isMockEnabled()) {
      return purchaseMock.handleReminder(reminderId, status, remark)
    }
    const response = await purchaseApi.handlePurchaseOverdueReminder({
      purchaseOverdueReminderHandleRequest: {
        remark: nullableText(remark),
        reminder_id: reminderId,
        status,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<PurchaseOverdueReminder | undefined>)
    return mapOptional(data, toReminder)
  },

  async listOrders(query: PurchaseOrderQuery) {
    const normalizedQuery = cleanQuery(query)
    if (isMockEnabled()) {
      return purchaseMock.listOrders(normalizedQuery)
    }
    if (normalizedQuery.orderId !== undefined) {
      throw new Error('当前后端接口暂不支持按采购订单编号查询')
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
    const payload = unwrap(response.data as ApiEnvelope<unknown>)
    return mapPageResult<PurchaseOrder, PurchaseOrderItem>(payload, normalizedQuery, toOrder)
  },

  async listReceipts(query: PurchaseReceiptQuery) {
    const normalizedQuery = cleanQuery(query)
    if (isMockEnabled()) {
      return purchaseMock.listReceipts(normalizedQuery)
    }
    const response = await purchaseApi.listPurchaseReceipt({
      materialId: normalizedQuery.materialId,
      orderId: normalizedQuery.orderId,
      page: normalizedQuery.page,
      pageSize: normalizedQuery.pageSize,
    })
    const payload = unwrap(response.data as ApiEnvelope<unknown>)
    return mapPageResult<PurchaseReceipt, PurchaseReceiptItem>(payload, normalizedQuery, toReceipt)
  },

  async listReminders(query: PurchaseReminderQuery) {
    const normalizedQuery = cleanQuery(query)
    if (isMockEnabled()) {
      return purchaseMock.listReminders(normalizedQuery)
    }
    const response = await purchaseApi.listPurchaseOverdueReminder({
      orderId: normalizedQuery.orderId,
      page: normalizedQuery.page,
      pageSize: normalizedQuery.pageSize,
      status: normalizedQuery.status,
    })
    const payload = unwrap(response.data as ApiEnvelope<unknown>)
    return mapPageResult<PurchaseOverdueReminder, PurchaseReminderItem>(
      payload,
      normalizedQuery,
      toReminder,
    )
  },

  async submitOrder(orderId: number, operatorId: number) {
    if (isMockEnabled()) {
      return purchaseMock.submitOrder(orderId, operatorId)
    }
    const response = await purchaseApi.submitPurchaseOrder({
      purchaseOrderActionRequest: { operator_id: operatorId, order_id: orderId },
    })
    const data = unwrap(response.data as ApiEnvelope<PurchaseOrder | undefined>)
    return mapOptional(data, toOrder)
  },

  async updateOrder(orderId: number, form: PurchaseOrderFormData) {
    validateOrderForm(form)
    if (isMockEnabled()) {
      return purchaseMock.updateOrder(orderId, form)
    }
    throw new Error('当前 OpenAPI 契约尚未提供采购草稿更新接口')
  },
}
