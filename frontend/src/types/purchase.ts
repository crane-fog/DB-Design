export type PurchaseOrderStatus =
  | 'cancelled'
  | 'completed'
  | 'draft'
  | 'partial_received'
  | 'submitted'

import type { PageRequest } from '@/services/pagination'

export type PurchaseReminderStatus = 'pending_urge' | 'received' | 'urged'

export interface SupplierInfo {
  contactPerson?: string
  contactPhone?: string
  isActive?: boolean
  supplierId: number
  supplierName: string
}

export interface PurchaseMaterialOption {
  defaultSupplierId?: number
  materialId: number
  materialName: string
  unit?: string
}

export interface PurchaseBuyerOption {
  buyerId: number
  buyerName: string
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

export interface PurchaseOrderQuery extends PageRequest {
  buyerId?: number
  expectedDateEnd?: string
  expectedDateStart?: string
  materialId?: number
  orderId?: number
  orderDateEnd?: string
  orderDateStart?: string
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

export interface PurchaseReceiptQuery extends PageRequest {
  materialId?: number
  orderId?: number
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

export interface PurchaseReminderQuery extends PageRequest {
  orderId?: number
  status?: PurchaseReminderStatus
}

export interface PurchaseOverviewSummary {
  overdueOrderCount: number
  pendingReminderCount: number
  receivingOrderCount: number
  totalOrderCount: number
}

export interface PurchaseReferenceData {
  buyers: PurchaseBuyerOption[]
  materials: PurchaseMaterialOption[]
  orders: PurchaseOrderItem[]
  suppliers: SupplierInfo[]
}
