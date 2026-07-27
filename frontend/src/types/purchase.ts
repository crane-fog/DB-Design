export type PurchaseOrderStatus =
  | 'cancelled'
  | 'completed'
  | 'draft'
  | 'partial_received'
  | 'submitted'

export type PurchaseReminderStatus = 'pending_urge' | 'received' | 'urged'

export interface SupplierInfo {
  contactPerson?: string
  contactPhone?: string
  supplierId: number
  supplierName: string
}

export interface PurchaseOrderLine {
  itemId: number
  lineAmount?: number
  materialId: number
  materialName?: string
  orderId: number
  quantity: number
  receiveProgress: number
  receivedQty: number
  unitPrice: number
}

export interface PurchaseOrderItem {
  actualDate?: string
  buyerId: number
  details: PurchaseOrderLine[]
  expectedDate: string
  isOverdue: boolean
  orderDate: string
  orderId: number
  overdueDays: number
  receiveProgress: number
  status: PurchaseOrderStatus
  supplier: SupplierInfo
  totalAmount: number
}

export interface PurchaseOrderQuery {
  buyerId?: number
  expectedDateEnd?: string
  expectedDateStart?: string
  materialId?: number
  orderDateEnd?: string
  orderDateStart?: string
  page: number
  pageSize: number
  status?: PurchaseOrderStatus
  supplierId?: number
}

export interface PurchaseOrderFormData {
  buyerId: number
  details: { materialId: number; quantity: number; unitPrice: number }[]
  expectedDate: string
  supplierId: number
}

export interface PurchaseDraftItem {
  materialId: number
  purchaseQty: number
  supplierId?: number
}

export interface PurchaseDraftResult {
  createdCount: number
  items: PurchaseOrderItem[]
  unassignedItems: { materialId: number; purchaseQty: number }[]
}

export interface PurchaseReceiptItem {
  materialId: number
  materialName?: string
  orderId: number
  quantity: number
  receiveDate: string
  receiveId: number
}

export interface PurchaseReceiptQuery {
  materialId?: number
  orderId?: number
  page: number
  pageSize: number
}

export interface PurchaseReceiptFormData {
  materialId: number
  orderId: number
  quantity: number
  receiveDate: string
}

export interface PurchaseReminderItem {
  expectedDate: string
  orderId: number
  overdueDays: number
  remindTime: string
  reminderId: number
  remark?: string
  status: PurchaseReminderStatus
}

export interface PurchaseReminderQuery {
  orderId?: number
  page: number
  pageSize: number
  status?: PurchaseReminderStatus
}

export interface PurchaseOverviewSummary {
  overdueOrderCount: number
  pendingReminderCount: number
  receivingOrderCount: number
  totalOrderCount: number
}
