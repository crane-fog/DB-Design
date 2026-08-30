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
  CapacityBalance,
  CapacityConfig,
  CapacityDetection,
  ExternalOrder,
  ExternalOrderConvertResult,
  FaultRecord,
  LineType,
  ProductionCalendar,
  ProductionCapacityEstimateResult,
  ProductionLine,
  ProductionLineStatus,
  ProductionOrderDetail,
} from '@/api'
import { isMockEnabled } from '@/config/mock'
import { productionApi } from '@/api/client'
import { productionRepository as productionMock } from '@/mock/repositories'
import { useAuthStore } from '@/stores/auth'

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
export type ExternalOrderStatusValue = ExternalOrder['status']

/** 故障记录状态。 */
export type FaultStatusValue = 'pending_repair' | 'recovered' | 'repairing'

/** 生产阶段状态。 */
export type ProductionStageStatus = 'completed' | 'in_progress' | 'pending' | 'paused'

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
  lastProgressRemark?: string
  lastProgressReportedAt?: string
  materialId: number
  materialName?: string
  orderId: number
  planEnd: string
  planQty: number
  planStart: string
  reviewComment?: string
  schedule?: ProductionScheduleItem
  status: ProductionOrderStatus
  versionId: number
  versionNo?: string
}

export interface ProductionProgressReportFormData {
  completedQty: number
  orderId: number
  remark?: string
  reportedAt?: string
}

export interface ProductionScheduleFormData {
  lineId: number
  orderId: number
  plannedEnd: string
  plannedStart: string
}

export interface ProductionScheduleItem {
  lineId: number
  lineName: string
  orderId: number
  plannedEnd: string
  plannedStart: string
  scheduleId: number
}

export interface ProductionStageItem {
  completedAt?: string
  name: string
  startedAt?: string
  status: ProductionStageStatus
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

export interface ExternalOrderCreateFormData {
  contactPerson: string
  contactPhone: string
  customerId?: number
  expectedDate: string
  materialId: number
  quantity: number
}

export interface ExternalOrderConvertFormData {
  extOrderId: number
  productionOrders: ProductionOrderFormData[]
}

export interface ExternalOrderConvertItem {
  associations: { extOrderId: number; orderId: number }[]
  extOrderId: number
  productionOrders: {
    finishedQty?: number
    materialId: number
    materialName?: string
    orderId: number
    planQty: number
    status: ProductionOrderStatus
  }[]
}

export interface ProductionCapacityEstimateFormData {
  expectedDate?: string
  materialId?: number
  orderId?: number
  planQty?: number
  versionId?: number
}

export interface ProductionCapacityEstimateItem {
  availableWorkMinutes?: number
  canDeliverOnTime?: boolean
  capacityReady?: boolean
  estimatedFinishDate?: string
  latestMaterialReadyDate?: string
  materialReady?: boolean
  requiredWorkMinutes?: number
  riskReason?: string
}

export interface CapacityDetectionFormData {
  lineId: number
  periodEnd: string
  periodStart: string
}

export interface CapacityDetectionItem {
  actualCapacity: number
  actualWorkHours?: number
  detectionId: number
  diffQty: number
  diffRate: number
  downtimeMinutes?: number
  efficiency?: number
  lineId: number
  periodEnd: string
  periodStart: string
  planCapacity: number
  reasonType: string
}

export interface CapacityBalanceFormData {
  affectedOrders: number[]
  afterPlan: Record<string, unknown>
  beforePlan: Record<string, unknown>
}

export interface CapacityBalanceItem {
  adjustTime: string
  affectedOrders: number[]
  afterPlan: Record<string, unknown>
  balanceId: number
  beforePlan: Record<string, unknown>
  operatorId: number
}

export interface ProductionLineStatusFormData {
  currentMaterialId?: number
  currentOrderId?: number
  efficiency?: number
  finishedQty?: number
  lineId: number
  status: ProductionLineRunStatus
}

export interface ProductionLineStatusItem {
  currentMaterialId?: number
  currentOrderId?: number
  efficiency: number
  finishedQty: number
  lineId: number
  status: ProductionLineRunStatus
  updatedTime: string
}

export interface FaultRecordItem {
  description: string
  faultId: number
  faultLevel: 'critical' | 'major' | 'minor'
  faultType: string
  lineId: number
  lineName?: string
  occurTime: string
  processingNote?: string
  recoverTime?: string
  repairerId?: number
  repairerName?: string
  reporterId: number
  reporterName?: string
  status: FaultStatusValue
}

export interface FaultRecordQuery extends PageRequest {
  faultType?: string
  lineId?: number
  occurEnd?: string
  occurStart?: string
  status?: FaultStatusValue
}

export interface FaultReportFormData {
  description: string
  faultLevel: FaultRecordItem['faultLevel']
  faultType: string
  lineId: number
  occurTime?: string
}

export interface FaultUpdateFormData {
  faultId: number
  recoverTime?: string
  repairerId?: number
  processingNote?: string
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
    faultLevel: 'major',
    faultType: record.fault_type,
    lineId: record.line_id,
    occurTime: record.occur_time,
    recoverTime: optionalText(record.recover_time),
    repairerId: record.repairer_id ?? undefined,
    reporterId: record.reporter_id,
    status: record.status,
  }
}

function toCapacityEstimate(
  result: ProductionCapacityEstimateResult,
): ProductionCapacityEstimateItem {
  return {
    availableWorkMinutes: result.available_work_minutes,
    canDeliverOnTime: result.can_deliver_on_time,
    capacityReady: result.capacity_ready,
    estimatedFinishDate: optionalText(result.estimated_finish_date),
    latestMaterialReadyDate: optionalText(result.latest_material_ready_date),
    materialReady: result.material_ready,
    requiredWorkMinutes: result.required_work_minutes,
    riskReason: optionalText(result.risk_reason),
  }
}

function toCapacityDetection(result: CapacityDetection): CapacityDetectionItem {
  return {
    actualCapacity: result.actual_capacity,
    actualWorkHours: result.actual_work_hours ?? undefined,
    detectionId: result.detection_id,
    diffQty: result.diff_qty,
    diffRate: result.diff_rate,
    downtimeMinutes: result.downtime_minutes ?? undefined,
    efficiency: result.efficiency ?? undefined,
    lineId: result.line_id,
    periodEnd: result.period_end,
    periodStart: result.period_start,
    planCapacity: result.plan_capacity,
    reasonType: result.reason_type,
  }
}

function toCapacityBalance(result: CapacityBalance): CapacityBalanceItem {
  return {
    adjustTime: result.adjust_time,
    affectedOrders: result.affected_orders,
    afterPlan: result.after_plan,
    balanceId: result.balance_id,
    beforePlan: result.before_plan,
    operatorId: result.operator_id,
  }
}

function toProductionLineStatus(result: ProductionLineStatus): ProductionLineStatusItem {
  return {
    currentMaterialId: result.current_material_id ?? undefined,
    currentOrderId: result.current_order_id ?? undefined,
    efficiency: result.efficiency,
    finishedQty: result.finished_qty,
    lineId: result.line_id,
    status: result.status,
    updatedTime: result.updated_time,
  }
}

function toExternalOrderConvert(result: ExternalOrderConvertResult): ExternalOrderConvertItem {
  return {
    associations: result.associations.map((association) => ({
      extOrderId: association.ext_order_id,
      orderId: association.order_id,
    })),
    extOrderId: result.ext_order_id,
    productionOrders: result.production_orders.map((order) => ({
      finishedQty: order.finished_qty,
      materialId: order.material_id,
      materialName: optionalText(order.material_name),
      orderId: order.order_id,
      planQty: order.plan_qty,
      status: order.status,
    })),
  }
}

function assertProductionMockIsReadOnly() {
  if (isMockEnabled()) {
    throw new Error('生产 Mock 当前为只读模式，暂不支持写操作。')
  }
}

function assertMockPermission(permission: string) {
  if (isMockEnabled() && !useAuthStore().hasPermission(permission)) {
    throw new Error('当前账号没有执行该生产管理操作的权限')
  }
}

export const productionService = {
  async addExternalOrder(form: ExternalOrderCreateFormData) {
    const auth = useAuthStore()
    let { customerId } = form
    if (auth.hasRole('外部客户')) {
      customerId = undefined
    }
    if (isMockEnabled()) {
      assertMockPermission('production:view')
      if (auth.hasRole('外部客户')) {
        customerId = auth.currentUser?.id
      }
      return productionMock.addExternalOrder({ ...form, customerId })
    }
    assertProductionMockIsReadOnly()
    const response = await productionApi.addExternalOrder({
      externalOrderCreateRequest: {
        contact_person: form.contactPerson.trim(),
        contact_phone: form.contactPhone.trim(),
        customer_id: customerId,
        expected_date: form.expectedDate,
        material_id: form.materialId,
        quantity: form.quantity,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<ExternalOrder | undefined>)
    if (data) {
      return toExternalOrder(data)
    }
    return undefined
  },

  api: productionApi,

  async approveOrder(orderId: number, approved: boolean, reviewComment?: string) {
    assertMockPermission('production:orders')
    if (isMockEnabled()) {
      return productionMock.approveOrder(orderId, approved, reviewComment)
    }
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
    assertMockPermission('production:orders')
    if (isMockEnabled()) {
      return productionMock.cancelOrder(orderId, remark)
    }
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

  async convertExternalOrder(form: ExternalOrderConvertFormData) {
    assertMockPermission('production:orders')
    if (isMockEnabled()) {
      return productionMock.convertExternalOrder(form)
    }
    assertProductionMockIsReadOnly()
    const response = await productionApi.convertExternalOrderToProductionOrder({
      externalOrderConvertRequest: {
        ext_order_id: form.extOrderId,
        production_orders: form.productionOrders.map((order) => ({
          material_id: order.materialId,
          plan_end: order.planEnd,
          plan_qty: order.planQty,
          plan_start: order.planStart,
          version_id: order.versionId,
        })),
      },
    })
    const data = unwrap(response.data as ApiEnvelope<ExternalOrderConvertResult | undefined>)
    if (data) {
      return toExternalOrderConvert(data)
    }
    return undefined
  },

  async createLine(form: ProductionLineFormData) {
    assertMockPermission('production:capacity')
    if (isMockEnabled()) {
      return productionMock.createLine(form)
    }
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
    assertMockPermission('production:orders')
    if (isMockEnabled()) {
      return productionMock.createOrder(form)
    }
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
    assertMockPermission('production:capacity')
    if (isMockEnabled()) {
      return productionMock.deleteCalendar(calendarDate, lineId)
    }
    assertProductionMockIsReadOnly()
    const response = await productionApi.deleteProductionCalendar({
      productionCalendarDeleteRequest: { calendar_date: calendarDate, line_id: lineId },
    })
    return unwrap(response.data as ApiEnvelope<unknown>)
  },

  async estimateCapacity(form: ProductionCapacityEstimateFormData) {
    assertMockPermission('production:capacity')
    if (isMockEnabled()) {
      return productionMock.estimateCapacity(form)
    }
    const response = await productionApi.estimateProductionCapacity({
      productionCapacityEstimateRequest: {
        expected_date: form.expectedDate,
        material_id: form.materialId,
        order_id: form.orderId,
        plan_qty: form.planQty,
        version_id: form.versionId,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<ProductionCapacityEstimateResult | undefined>)
    if (data) {
      return toCapacityEstimate(data)
    }
    return undefined
  },

  async finishOrder(orderId: number, finishedQty: number, remark?: string) {
    assertMockPermission('production:orders')
    if (isMockEnabled()) {
      return productionMock.finishOrder(orderId, finishedQty)
    }
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

  async getFault(faultId: number): Promise<FaultRecordItem | undefined> {
    if (isMockEnabled()) {
      return productionMock.getFault(faultId)
    }
    throw new Error('当前后端暂未提供生产故障详情接口')
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

  async getOrderStages(orderId: number): Promise<ProductionStageItem[]> {
    if (isMockEnabled()) {
      return productionMock.getOrderStages(orderId)
    }
    throw new Error('当前后端暂未提供生产阶段查询接口')
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
      const auth = useAuthStore()
      if (auth.hasRole('外部客户')) {
        return productionMock.listExternalOrders({
          ...query,
          customerId: auth.currentUser?.id,
        })
      }
      if (!auth.hasPermission('production:orders')) {
        throw new Error('当前账号没有查看外部订单的权限')
      }
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

  async listFaults(query: FaultRecordQuery): Promise<PageResult<FaultRecordItem>> {
    if (isMockEnabled()) {
      return productionMock.listFaults(query)
    }
    throw new Error('当前后端暂未提供生产故障列表接口')
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
    assertMockPermission('production:breakdown')
    if (isMockEnabled()) {
      return productionMock.reportFault(form)
    }
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

  async reportOrderProgress(form: ProductionProgressReportFormData) {
    assertMockPermission('production:orders')
    if (isMockEnabled()) {
      return productionMock.reportOrderProgress(form)
    }
    throw new Error('当前后端暂未提供生产进度上报接口')
  },

  async reviewExternalOrder(extOrderId: number, accepted: boolean, reviewComment?: string) {
    assertMockPermission('production:orders')
    if (isMockEnabled()) {
      return productionMock.reviewExternalOrder(extOrderId, accepted, reviewComment)
    }
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

  async runCapacityDetection(form: CapacityDetectionFormData) {
    assertMockPermission('production:capacity')
    if (isMockEnabled()) {
      return productionMock.runCapacityDetection(form)
    }
    const response = await productionApi.runCapacityDetection({
      capacityDetectionRunRequest: {
        line_id: form.lineId,
        period_end: form.periodEnd,
        period_start: form.periodStart,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<CapacityDetection | undefined>)
    if (data) {
      return toCapacityDetection(data)
    }
    return undefined
  },

  async saveCalendar(form: ProductionCalendarFormData) {
    assertMockPermission('production:capacity')
    if (isMockEnabled()) {
      return productionMock.saveCalendar(form)
    }
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

  async saveCapacityBalance(form: CapacityBalanceFormData) {
    assertMockPermission('production:capacity')
    if (isMockEnabled()) {
      return productionMock.saveCapacityBalance(form)
    }
    const response = await productionApi.saveCapacityBalance({
      capacityBalanceSaveRequest: {
        affected_orders: form.affectedOrders,
        after_plan: form.afterPlan,
        before_plan: form.beforePlan,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<CapacityBalance | undefined>)
    if (data) {
      return toCapacityBalance(data)
    }
    return undefined
  },

  async saveCapacityConfig(form: CapacityConfigFormData) {
    assertMockPermission('production:capacity')
    if (isMockEnabled()) {
      return productionMock.saveCapacityConfig(form)
    }
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
    assertMockPermission('production:capacity')
    if (isMockEnabled()) {
      return productionMock.saveLineType(form)
    }
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

  async saveOrderSchedule(form: ProductionScheduleFormData) {
    assertMockPermission('production:orders')
    if (isMockEnabled()) {
      return productionMock.saveOrderSchedule(form)
    }
    throw new Error('当前后端暂未提供生产订单排产接口')
  },

  async startOrder(orderId: number, remark?: string) {
    assertMockPermission('production:orders')
    if (isMockEnabled()) {
      return productionMock.startOrder(orderId)
    }
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
    assertMockPermission('production:breakdown')
    if (isMockEnabled()) {
      return productionMock.updateFault(form)
    }
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
    assertMockPermission('production:capacity')
    if (isMockEnabled()) {
      return productionMock.updateLine(lineId, form)
    }
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

  async updateLineStatus(form: ProductionLineStatusFormData) {
    assertMockPermission('production:capacity')
    if (isMockEnabled()) {
      return productionMock.updateLineStatus(form)
    }
    const response = await productionApi.updateProductionLineStatus({
      productionLineStatusUpdateRequest: {
        current_material_id: form.currentMaterialId,
        current_order_id: form.currentOrderId,
        efficiency: form.efficiency,
        finished_qty: form.finishedQty,
        line_id: form.lineId,
        status: form.status,
      },
    })
    const data = unwrap(response.data as ApiEnvelope<ProductionLineStatus | undefined>)
    if (data) {
      return toProductionLineStatus(data)
    }
    return undefined
  },

  async updateOrder(orderId: number, form: ProductionOrderFormData) {
    assertMockPermission('production:orders')
    if (isMockEnabled()) {
      return productionMock.updateOrder(orderId, form)
    }
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
