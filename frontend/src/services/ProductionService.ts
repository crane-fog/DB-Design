import {
  type ApiEnvelope,
  type PageRequest,
  type PageResult,
  getPageItems,
  getPageMetadata,
  nullableText,
  optionalText,
  unwrap,
} from '@/services/pagination'
import type {
  CapacityConfig,
  ExternalOrder,
  FaultRecord,
  LineType,
  ProductionCalendar,
  ProductionLine,
  ProductionOrderDetail,
} from '@/api'
import { isMockEnabled } from '@/config/mock'
import { productionApi } from '@/api/client'
import { productionMock } from '@/config/production-mock'

export type { PageResult }

/** 生产订单状态。 */
export type ProductionOrderStatus =
  | 'cancelled'
  | 'completed'
  | 'in_progress'
  | 'pending_review'
  | 'pending_schedule'

/** 生产线运行状态。 */
export type ProductionLineRunStatus = 'fault' | 'idle' | 'running'

/** 外部订单状态。 */
export type ExternalOrderStatusValue = 'accepted' | 'pending_review' | 'rejected'

/** 故障记录状态。 */
export type FaultStatusValue = 'pending_repair' | 'recovered' | 'repairing'

export interface ProductionOrderQuery extends PageRequest {
  materialId?: number
  planEndEnd?: string
  planEndStart?: string
  status?: ProductionOrderStatus
}

export interface ProductionLineQuery extends PageRequest {
  status?: ProductionLineRunStatus
  typeId?: number
}

export interface CapacityConfigQuery extends PageRequest {
  materialId?: number
  typeId?: number
}

export interface ProductionCalendarQuery extends PageRequest {
  calendarDateEnd?: string
  calendarDateStart?: string
  configId?: number
  lineId?: number
}

export interface LineTypeQuery extends PageRequest {
  typeName?: string
}

export interface ExternalOrderQuery extends PageRequest {
  customerId?: number
  status?: ExternalOrderStatusValue
}

export interface ProductionOrderItem {
  actualEnd?: string
  actualStart?: string
  finishedQty?: number
  materialId: number
  materialName?: string
  orderId: number
  planEnd: string
  planQty: number
  planStart: string
  reviewComment?: string
  status: ProductionOrderStatus
  versionId: number
  versionNo?: string
}

export interface ProductionOrderFormData {
  materialId: number
  planEnd: string
  planQty: number
  planStart: string
  versionId: number
}

export interface ProductionLineItem {
  lineId: number
  managerId: number
  managerName?: string
  startDate: string
  status?: ProductionLineRunStatus
  typeId: number
  typeName?: string
}

export interface ProductionLineFormData {
  managerId: number
  startDate: string
  typeId: number
}

export interface CapacityConfigItem {
  configId: number
  materialId: number
  materialName?: string
  typeId: number
  typeName?: string
  unitTime: number
}

export interface CapacityConfigFormData {
  configId?: number
  materialId: number
  typeId: number
  unitTime: number
}

export interface ProductionCalendarItem {
  calendarDate: string
  configId: number
  lineId: number
  lineName?: string
  materialId?: number
  materialName?: string
  typeId?: number
  typeName?: string
}

export interface ProductionCalendarFormData {
  calendarDate: string
  configId: number
  lineId: number
}

export interface LineTypeItem {
  typeId: number
  typeName: string
}

export interface LineTypeFormData {
  typeId?: number
  typeName: string
}

export interface ExternalOrderItem {
  contactPerson: string
  contactPhone: string
  customerId: number
  customerName?: string
  expectedDate: string
  extOrderId: number
  materialId: number
  materialName?: string
  quantity: number
  reviewComment?: string
  status: ExternalOrderStatusValue
  submitTime: string
}

export interface FaultRecordItem {
  description: string
  faultId: number
  faultType: string
  lineId: number
  occurTime: string
  recoverTime?: string
  repairerId?: number
  reporterId: number
  status: FaultStatusValue
}

export interface FaultReportFormData {
  description: string
  faultType: string
  lineId: number
}

export interface FaultUpdateFormData {
  faultId: number
  recoverTime?: string
  repairerId?: number
  status: FaultStatusValue
}

function toProductionOrder(order: ProductionOrderDetail): ProductionOrderItem {
  return {
    actualEnd: optionalText(order.actual_end),
    actualStart: optionalText(order.actual_start),
    finishedQty: order.finished_qty,
    materialId: order.material_id,
    materialName: optionalText(order.material_name),
    orderId: order.order_id,
    planEnd: order.plan_end,
    planQty: order.plan_qty,
    planStart: order.plan_start,
    reviewComment: optionalText(order.review_comment),
    status: order.status,
    versionId: order.version_id,
    versionNo: optionalText(order.version_no),
  }
}

function toProductionLine(line: ProductionLine): ProductionLineItem {
  return {
    lineId: line.line_id,
    managerId: line.manager_id,
    managerName: optionalText(line.manager_name),
    startDate: line.start_date,
    status: line.status,
    typeId: line.type_id,
    typeName: optionalText(line.type_name),
  }
}

function toCapacityConfig(config: CapacityConfig): CapacityConfigItem {
  return {
    configId: config.config_id,
    materialId: config.material_id,
    materialName: optionalText(config.material_name),
    typeId: config.type_id,
    typeName: optionalText(config.type_name),
    unitTime: config.unit_time,
  }
}

function toProductionCalendar(calendar: ProductionCalendar): ProductionCalendarItem {
  return {
    calendarDate: calendar.calendar_date,
    configId: calendar.config_id,
    lineId: calendar.line_id,
    lineName: optionalText(calendar.line_name),
    materialId: calendar.material_id ?? undefined,
    materialName: optionalText(calendar.material_name),
    typeId: calendar.type_id ?? undefined,
    typeName: optionalText(calendar.type_name),
  }
}

function toLineType(type: LineType): LineTypeItem {
  return {
    typeId: type.type_id,
    typeName: type.type_name,
  }
}

function toExternalOrder(order: ExternalOrder): ExternalOrderItem {
  return {
    contactPerson: order.contact_person,
    contactPhone: order.contact_phone,
    customerId: order.customer_id,
    customerName: optionalText(order.customer_name),
    expectedDate: order.expected_date,
    extOrderId: order.ext_order_id,
    materialId: order.material_id,
    materialName: optionalText(order.material_name),
    quantity: order.quantity,
    reviewComment: optionalText(order.review_comment),
    status: order.status,
    submitTime: order.submit_time,
  }
}

function toFaultRecord(record: FaultRecord): FaultRecordItem {
  return {
    description: record.description,
    faultId: record.fault_id,
    faultType: record.fault_type,
    lineId: record.line_id,
    occurTime: record.occur_time,
    recoverTime: optionalText(record.recover_time),
    repairerId: record.repairer_id ?? undefined,
    reporterId: record.reporter_id,
    status: record.status,
  }
}

function assertProductionMockIsReadOnly() {
  if (isMockEnabled()) {
    throw new Error('生产 Mock 当前为只读模式，暂不支持写操作。')
  }
}

export const productionService = {
  api: productionApi,

  async approveOrder(orderId: number, approved: boolean, reviewComment?: string) {
    assertProductionMockIsReadOnly()
    const response = await productionApi.approveProductionOrder({
      productionOrderApproveRequest: {
        approved,
        order_id: orderId,
        review_comment: nullableText(reviewComment),
      },
    })
    const data = unwrap(response.data as ApiEnvelope<ProductionOrderDetail | undefined>)
    if (data) {
      return toProductionOrder(data)
    }
    return undefined
  },

  async cancelOrder(orderId: number, remark?: string) {
    assertProductionMockIsReadOnly()
    const response = await productionApi.cancelProductionOrder({
      productionOrderActionRequest: { order_id: orderId, remark: nullableText(remark) },
    })
    const data = unwrap(response.data as ApiEnvelope<ProductionOrderDetail | undefined>)
    if (data) {
      return toProductionOrder(data)
    }
    return undefined
  },

  async createLine(form: ProductionLineFormData) {
    assertProductionMockIsReadOnly()
    const response = await productionApi.addProductionLine({
      productionLineCreateRequest: {
        manager_id: form.managerId,
        start_date: form.startDate,
        type_id: form.typeId,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<ProductionLine | undefined>)
    if (data) {
      return toProductionLine(data)
    }
    return undefined
  },

  async createOrder(form: ProductionOrderFormData) {
    assertProductionMockIsReadOnly()
    const response = await productionApi.addProductionOrder({
      productionOrderCreateRequest: {
        material_id: form.materialId,
        plan_end: form.planEnd,
        plan_qty: form.planQty,
        plan_start: form.planStart,
        version_id: form.versionId,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<ProductionOrderDetail | undefined>)
    if (data) {
      return toProductionOrder(data)
    }
    return undefined
  },

  async deleteCalendar(calendarDate: string, lineId: number) {
    assertProductionMockIsReadOnly()
    const response = await productionApi.deleteProductionCalendar({
      productionCalendarDeleteRequest: { calendar_date: calendarDate, line_id: lineId },
    })
    return unwrap(response.data as ApiEnvelope<unknown>)
  },

  async finishOrder(orderId: number, finishedQty: number, remark?: string) {
    assertProductionMockIsReadOnly()
    const response = await productionApi.finishProductionOrder({
      productionOrderFinishRequest: {
        finished_qty: finishedQty,
        order_id: orderId,
        remark: nullableText(remark),
      },
    })
    const data = unwrap(response.data as ApiEnvelope<ProductionOrderDetail | undefined>)
    if (data) {
      return toProductionOrder(data)
    }
    return undefined
  },

  async getOrder(orderId: number) {
    if (isMockEnabled()) {
      return productionMock.getOrder(orderId)
    }
    const response = await productionApi.getProductionOrder({ orderId })
    const data = unwrap(response.data as ApiEnvelope<ProductionOrderDetail | undefined>)
    if (data) {
      return toProductionOrder(data)
    }
    return undefined
  },

  async listAllLineTypes(): Promise<LineTypeItem[]> {
    if (isMockEnabled()) {
      return productionMock.listAllLineTypes()
    }
    const response = await productionApi.listProductionLineType({ page: 1, pageSize: 100 })
    const data = unwrap(response.data as ApiEnvelope<unknown>)
    return getPageItems<LineType>(data).map(toLineType)
  },

  async listCalendars(query: ProductionCalendarQuery): Promise<PageResult<ProductionCalendarItem>> {
    if (isMockEnabled()) {
      return productionMock.listCalendars(query)
    }
    const response = await productionApi.listProductionCalendar({
      calendarDateEnd: query.calendarDateEnd,
      calendarDateStart: query.calendarDateStart,
      configId: query.configId,
      lineId: query.lineId,
      page: query.page,
      pageSize: query.pageSize,
    })
    const data = unwrap(response.data as ApiEnvelope<unknown>)
    const items = getPageItems<ProductionCalendar>(data).map(toProductionCalendar)
    const metadata = getPageMetadata(data, {
      page: query.page,
      pageSize: query.pageSize,
      total: items.length,
    })
    return { items, ...metadata }
  },

  async listCapacityConfigs(query: CapacityConfigQuery): Promise<PageResult<CapacityConfigItem>> {
    if (isMockEnabled()) {
      return productionMock.listCapacityConfigs(query)
    }
    const response = await productionApi.listCapacityConfig({
      materialId: query.materialId,
      page: query.page,
      pageSize: query.pageSize,
      typeId: query.typeId,
    })
    const data = unwrap(response.data as ApiEnvelope<unknown>)
    const items = getPageItems<CapacityConfig>(data).map(toCapacityConfig)
    const metadata = getPageMetadata(data, {
      page: query.page,
      pageSize: query.pageSize,
      total: items.length,
    })
    return { items, ...metadata }
  },

  async listExternalOrders(query: ExternalOrderQuery): Promise<PageResult<ExternalOrderItem>> {
    if (isMockEnabled()) {
      return productionMock.listExternalOrders(query)
    }
    const response = await productionApi.listExternalOrder({
      customerId: query.customerId,
      page: query.page,
      pageSize: query.pageSize,
      status: query.status,
    })
    const data = unwrap(response.data as ApiEnvelope<unknown>)
    const items = getPageItems<ExternalOrder>(data).map(toExternalOrder)
    const metadata = getPageMetadata(data, {
      page: query.page,
      pageSize: query.pageSize,
      total: items.length,
    })
    return { items, ...metadata }
  },

  async listLineTypes(query: LineTypeQuery): Promise<PageResult<LineTypeItem>> {
    if (isMockEnabled()) {
      return productionMock.listLineTypes(query)
    }
    const response = await productionApi.listProductionLineType({
      page: query.page,
      pageSize: query.pageSize,
      typeName: query.typeName || undefined,
    })
    const data = unwrap(response.data as ApiEnvelope<unknown>)
    const items = getPageItems<LineType>(data).map(toLineType)
    const metadata = getPageMetadata(data, {
      page: query.page,
      pageSize: query.pageSize,
      total: items.length,
    })
    return { items, ...metadata }
  },

  async listLines(query: ProductionLineQuery): Promise<PageResult<ProductionLineItem>> {
    if (isMockEnabled()) {
      return productionMock.listLines(query)
    }
    const response = await productionApi.listProductionLine({
      page: query.page,
      pageSize: query.pageSize,
      status: query.status,
      typeId: query.typeId,
    })
    const data = unwrap(response.data as ApiEnvelope<unknown>)
    const items = getPageItems<ProductionLine>(data).map(toProductionLine)
    const metadata = getPageMetadata(data, {
      page: query.page,
      pageSize: query.pageSize,
      total: items.length,
    })
    return { items, ...metadata }
  },

  async listOrders(query: ProductionOrderQuery): Promise<PageResult<ProductionOrderItem>> {
    if (isMockEnabled()) {
      return productionMock.listOrders(query)
    }
    const response = await productionApi.listProductionOrder({
      materialId: query.materialId,
      page: query.page,
      pageSize: query.pageSize,
      planEndEnd: query.planEndEnd,
      planEndStart: query.planEndStart,
      status: query.status,
    })
    const data = unwrap(response.data as ApiEnvelope<unknown>)
    const items = getPageItems<ProductionOrderDetail>(data).map(toProductionOrder)
    const metadata = getPageMetadata(data, {
      page: query.page,
      pageSize: query.pageSize,
      total: items.length,
    })
    return { items, ...metadata }
  },

  async reportFault(form: FaultReportFormData) {
    assertProductionMockIsReadOnly()
    const response = await productionApi.reportProductionLineFault({
      faultRecordCreateRequest: {
        description: form.description.trim(),
        fault_type: form.faultType.trim(),
        line_id: form.lineId,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<FaultRecord | undefined>)
    if (data) {
      return toFaultRecord(data)
    }
    return undefined
  },

  async reviewExternalOrder(extOrderId: number, accepted: boolean, reviewComment?: string) {
    assertProductionMockIsReadOnly()
    const response = await productionApi.reviewExternalOrder({
      externalOrderReviewRequest: {
        accepted,
        ext_order_id: extOrderId,
        review_comment: nullableText(reviewComment),
      },
    })
    const data = unwrap(response.data as ApiEnvelope<ExternalOrder | undefined>)
    if (data) {
      return toExternalOrder(data)
    }
    return undefined
  },

  async saveCalendar(form: ProductionCalendarFormData) {
    assertProductionMockIsReadOnly()
    const response = await productionApi.saveProductionCalendar({
      productionCalendarSaveRequest: {
        calendar_date: form.calendarDate,
        config_id: form.configId,
        line_id: form.lineId,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<ProductionCalendar | undefined>)
    if (data) {
      return toProductionCalendar(data)
    }
    return undefined
  },

  async saveCapacityConfig(form: CapacityConfigFormData) {
    assertProductionMockIsReadOnly()
    const response = await productionApi.saveCapacityConfig({
      capacityConfigSaveRequest: {
        config_id: form.configId ?? undefined,
        material_id: form.materialId,
        type_id: form.typeId,
        unit_time: form.unitTime,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<CapacityConfig | undefined>)
    if (data) {
      return toCapacityConfig(data)
    }
    return undefined
  },

  async saveLineType(form: LineTypeFormData) {
    assertProductionMockIsReadOnly()
    const response = await productionApi.saveProductionLineType({
      lineTypeSaveRequest: { type_id: form.typeId ?? undefined, type_name: form.typeName.trim() },
    })
    const data = unwrap(response.data as ApiEnvelope<LineType | undefined>)
    if (data) {
      return toLineType(data)
    }
    return undefined
  },

  async startOrder(orderId: number, remark?: string) {
    assertProductionMockIsReadOnly()
    const response = await productionApi.startProductionOrder({
      productionOrderActionRequest: { order_id: orderId, remark: nullableText(remark) },
    })
    const data = unwrap(response.data as ApiEnvelope<ProductionOrderDetail | undefined>)
    if (data) {
      return toProductionOrder(data)
    }
    return undefined
  },

  async updateFault(form: FaultUpdateFormData) {
    assertProductionMockIsReadOnly()
    const response = await productionApi.updateProductionLineFault({
      faultRecordUpdateRequest: {
        fault_id: form.faultId,
        recover_time: nullableText(form.recoverTime),
        repairer_id: form.repairerId ?? undefined,
        status: form.status,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<FaultRecord | undefined>)
    if (data) {
      return toFaultRecord(data)
    }
    return undefined
  },

  async updateLine(lineId: number, form: ProductionLineFormData) {
    assertProductionMockIsReadOnly()
    const response = await productionApi.updateProductionLine({
      productionLineUpdateRequest: {
        line_id: lineId,
        manager_id: form.managerId,
        start_date: form.startDate,
        type_id: form.typeId,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<ProductionLine | undefined>)
    if (data) {
      return toProductionLine(data)
    }
    return undefined
  },

  async updateOrder(orderId: number, form: ProductionOrderFormData) {
    assertProductionMockIsReadOnly()
    const response = await productionApi.updateProductionOrder({
      productionOrderUpdateRequest: {
        material_id: form.materialId,
        order_id: orderId,
        plan_end: form.planEnd,
        plan_qty: form.planQty,
        plan_start: form.planStart,
        version_id: form.versionId,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<ProductionOrderDetail | undefined>)
    if (data) {
      return toProductionOrder(data)
    }
    return undefined
  },
}
