<script setup lang="ts">
import { Bell, CirclePlus, Document, Plus, Refresh, Search, Van } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import type {
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
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { formatAmount, formatDateTime, formatNumber } from '@/utils/format'
import {
  purchaseOrderStatusLabels as orderStatusLabels,
  purchaseReminderStatusLabels as reminderStatusLabels,
} from '@/constants/status'
import EmptyState from '@/components/common/EmptyState.vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import StatusTag from '@/components/common/StatusTag.vue'
import { getErrorMessage } from '@/utils/error'
import { purchaseService } from '@/services/PurchaseService'
import { useAuthStore } from '@/stores/auth'

type PurchaseTab = 'orders' | 'receipts' | 'reminders'

const orderActionConfig = {
  cancel: {
    confirmButtonText: '确认取消',
    errorMessage: '取消订单失败',
    message: (orderId: number) => `取消后订单 #${orderId} 将无法继续收货，确认继续？`,
    request: (orderId: number, operator: number) => purchaseService.cancelOrder(orderId, operator),
    successMessage: '采购订单已取消',
    title: '取消采购订单',
    type: 'warning' as const,
  },
  submit: {
    confirmButtonText: '确认提交',
    errorMessage: '提交订单失败',
    message: (orderId: number) => `确认提交采购订单 #${orderId}？提交后将进入到货跟踪。`,
    request: (orderId: number, operator: number) => purchaseService.submitOrder(orderId, operator),
    successMessage: '采购订单已提交',
    title: '提交采购订单',
    type: 'info' as const,
  },
}

const auth = useAuthStore()
const operatorId = computed(() => auth.currentUser?.id)
const activeTab = ref<PurchaseTab>('orders')
const summary = ref<PurchaseOverviewSummary>()
const summaryLoading = ref(false)
const summaryError = ref('')
let alive = true
let summaryRequestId = 0

// 采购订单
const orderLoading = ref(false)
const orderError = ref('')
const orderItems = ref<PurchaseOrderItem[]>([])
const orderTotal = ref(0)
const orderDateRange = ref<[string, string]>()
const expectedDateRange = ref<[string, string]>()
const orderQuery = reactive<PurchaseOrderQuery>({ page: 1, pageSize: 10 })
const orderDialogOpen = ref(false)
const orderSubmitting = ref(false)
const orderFormRef = ref<FormInstance>()
const orderForm = reactive<PurchaseOrderFormData>({
  buyerId: 0,
  details: [{ materialId: 0, quantity: 1, unitPrice: 0 }],
  expectedDate: '',
  supplierId: 0,
})
const orderRules: FormRules<PurchaseOrderFormData> = {
  buyerId: [{ message: '请选择采购员', required: true, trigger: 'change', type: 'number' }],
  expectedDate: [{ message: '请选择预计交货日期', required: true, trigger: 'change' }],
  supplierId: [{ message: '请选择供应商', required: true, trigger: 'change', type: 'number' }],
}
const actingOrderId = ref<number>()
const detailDrawerOpen = ref(false)
const detailLoading = ref(false)
const selectedOrder = ref<PurchaseOrderItem>()
const detailReceipts = ref<PurchaseReceiptItem[]>([])
const detailReminders = ref<PurchaseReminderItem[]>([])
let detailRequestId = 0
let orderRequestId = 0

// 采购关联选项
const referenceData = ref<PurchaseReferenceData>({
  buyers: [],
  materials: [],
  orders: [],
  suppliers: [],
})
const referenceLoading = ref(false)
const referenceError = ref('')
let referenceRequestId = 0

// 收货记录
const receiptLoading = ref(false)
const receiptError = ref('')
const receiptItems = ref<PurchaseReceiptItem[]>([])
const receiptTotal = ref(0)
const receiptQuery = reactive<PurchaseReceiptQuery>({ page: 1, pageSize: 10 })
const receiptDialogOpen = ref(false)
const receiptSubmitting = ref(false)
const receiptFormRef = ref<FormInstance>()
const receiptForm = reactive<PurchaseReceiptFormData>({
  materialId: 0,
  orderId: 0,
  quantity: 1,
  receiveDate: new Date().toISOString().slice(0, 10),
})
const receiptRules: FormRules<PurchaseReceiptFormData> = {
  materialId: [
    { message: '请输入物料 ID', required: true, trigger: 'blur', type: 'number' },
    { message: '物料 ID 必须大于 0', min: 1, trigger: 'blur', type: 'number' },
  ],
  orderId: [
    { message: '请输入采购订单 ID', required: true, trigger: 'blur', type: 'number' },
    { message: '订单 ID 必须大于 0', min: 1, trigger: 'blur', type: 'number' },
  ],
  quantity: [
    { message: '请输入本次收货数量', required: true, trigger: 'blur', type: 'number' },
    { message: '收货数量必须大于 0', min: 0.01, trigger: 'blur', type: 'number' },
  ],
  receiveDate: [{ message: '请选择收货日期', required: true, trigger: 'change' }],
}
let receiptRequestId = 0

// 逾期提醒
const reminderLoading = ref(false)
const reminderError = ref('')
const reminderItems = ref<PurchaseReminderItem[]>([])
const reminderTotal = ref(0)
const reminderQuery = reactive<PurchaseReminderQuery>({ page: 1, pageSize: 10 })
const generatingReminders = ref(false)
const reminderDialogOpen = ref(false)
const reminderSubmitting = ref(false)
const reminderTarget = ref<PurchaseReminderItem>()
const reminderForm = reactive<{ remark: string; status: 'received' | 'urged' }>({
  remark: '',
  status: 'urged',
})
let reminderRequestId = 0

const statistics = computed(() => [
  { label: '采购订单总数', tone: 'blue', value: summary.value?.totalOrderCount },
  { label: '待到货订单', tone: 'pink', value: summary.value?.receivingOrderCount },
  { label: '逾期订单', tone: 'blue', value: summary.value?.overdueOrderCount },
  { label: '待催交提醒', tone: 'pink', value: summary.value?.pendingReminderCount },
])

const orderTotalAmount = computed(() =>
  orderForm.details.reduce((sum, item) => sum + item.quantity * item.unitPrice, 0),
)
const receivableOrders = computed(() =>
  referenceData.value.orders.filter((order) =>
    ['partial_received', 'submitted'].includes(order.status),
  ),
)
const activeSuppliers = computed(() =>
  referenceData.value.suppliers.filter((supplier) => supplier.isActive !== false),
)
const selectedReceiptOrder = computed(() =>
  receivableOrders.value.find((order) => order.orderId === receiptForm.orderId),
)
const receiptMaterialOptions = computed(
  () =>
    selectedReceiptOrder.value?.details.filter((line) => line.receivedQty < line.quantity) ?? [],
)
const receiptRemainingQty = computed(() => {
  const line = receiptMaterialOptions.value.find(
    (item) => item.materialId === receiptForm.materialId,
  )
  if (line) {
    return line.quantity - line.receivedQty
  }
  return undefined
})
const refreshing = computed(
  () => summaryLoading.value || orderLoading.value || receiptLoading.value || reminderLoading.value,
)
const reminderStatusOptions = computed(() => {
  if (reminderTarget.value?.status === 'urged') {
    return [{ label: '确认到货', value: 'received' }]
  }
  return [
    { label: '已催交', value: 'urged' },
    { label: '确认到货', value: 'received' },
  ]
})

async function loadSummary() {
  const currentRequestId = ++summaryRequestId
  summaryLoading.value = true
  summaryError.value = ''
  try {
    const result = await purchaseService.getOverview()
    if (alive && currentRequestId === summaryRequestId) {
      summary.value = result
    }
  } catch (error) {
    if (alive && currentRequestId === summaryRequestId) {
      summary.value = undefined
      summaryError.value = getErrorMessage(error, '采购概览加载失败')
    }
  } finally {
    if (alive && currentRequestId === summaryRequestId) {
      summaryLoading.value = false
    }
  }
}

async function loadReferenceData() {
  const currentRequestId = ++referenceRequestId
  referenceLoading.value = true
  referenceError.value = ''
  try {
    const result = await purchaseService.getReferenceData()
    if (alive && currentRequestId === referenceRequestId) {
      referenceData.value = result
    }
  } catch (error) {
    if (alive && currentRequestId === referenceRequestId) {
      referenceError.value = getErrorMessage(error, '采购关联数据加载失败')
    }
  } finally {
    if (alive && currentRequestId === referenceRequestId) {
      referenceLoading.value = false
    }
  }
}

async function loadOrders() {
  const currentRequestId = ++orderRequestId
  orderLoading.value = true
  orderError.value = ''
  try {
    const result = await purchaseService.listOrders({
      ...orderQuery,
      expectedDateEnd: expectedDateRange.value?.[1],
      expectedDateStart: expectedDateRange.value?.[0],
      orderDateEnd: orderDateRange.value?.[1],
      orderDateStart: orderDateRange.value?.[0],
    })
    if (!alive || currentRequestId !== orderRequestId) {
      return
    }
    orderItems.value = result.items
    orderTotal.value = result.total
  } catch (error) {
    if (alive && currentRequestId === orderRequestId) {
      orderError.value = getErrorMessage(error, '采购订单加载失败')
    }
  } finally {
    if (alive && currentRequestId === orderRequestId) {
      orderLoading.value = false
    }
  }
}

async function loadReceipts() {
  const currentRequestId = ++receiptRequestId
  receiptLoading.value = true
  receiptError.value = ''
  try {
    const result = await purchaseService.listReceipts(receiptQuery)
    if (!alive || currentRequestId !== receiptRequestId) {
      return
    }
    receiptItems.value = result.items
    receiptTotal.value = result.total
  } catch (error) {
    if (alive && currentRequestId === receiptRequestId) {
      receiptError.value = getErrorMessage(error, '收货记录加载失败')
    }
  } finally {
    if (alive && currentRequestId === receiptRequestId) {
      receiptLoading.value = false
    }
  }
}

async function loadReminders() {
  const currentRequestId = ++reminderRequestId
  reminderLoading.value = true
  reminderError.value = ''
  try {
    const result = await purchaseService.listReminders(reminderQuery)
    if (!alive || currentRequestId !== reminderRequestId) {
      return
    }
    reminderItems.value = result.items
    reminderTotal.value = result.total
  } catch (error) {
    if (alive && currentRequestId === reminderRequestId) {
      reminderError.value = getErrorMessage(error, '逾期提醒加载失败')
    }
  } finally {
    if (alive && currentRequestId === reminderRequestId) {
      reminderLoading.value = false
    }
  }
}

function loadActiveTab() {
  if (activeTab.value === 'orders') {
    void loadOrders()
  }
  if (activeTab.value === 'receipts') {
    void loadReceipts()
  }
  if (activeTab.value === 'reminders') {
    void loadReminders()
  }
}

function refreshAll() {
  void loadSummary()
  loadActiveTab()
}

function resetOrderQuery() {
  Object.assign(orderQuery, {
    buyerId: undefined,
    materialId: undefined,
    orderId: undefined,
    page: 1,
    status: undefined,
    supplierId: undefined,
  })
  orderDateRange.value = undefined
  expectedDateRange.value = undefined
  void loadOrders()
}

function resetReceiptQuery() {
  Object.assign(receiptQuery, { materialId: undefined, orderId: undefined, page: 1 })
  void loadReceipts()
}

function resetReminderQuery() {
  Object.assign(reminderQuery, { orderId: undefined, page: 1, status: undefined })
  void loadReminders()
}

function searchOrders() {
  orderQuery.page = 1
  void loadOrders()
}

function searchReceipts() {
  receiptQuery.page = 1
  void loadReceipts()
}

function searchReminders() {
  reminderQuery.page = 1
  void loadReminders()
}

function openOrderDialog() {
  Object.assign(orderForm, {
    buyerId: operatorId.value ?? referenceData.value.buyers[0]?.buyerId ?? 0,
    details: [{ materialId: 0, quantity: 1, unitPrice: 0 }],
    expectedDate: '',
    supplierId: 0,
  })
  orderDialogOpen.value = true
}

function addOrderLine() {
  orderForm.details.push({ materialId: 0, quantity: 1, unitPrice: 0 })
}

function removeOrderLine(index: number) {
  if (orderForm.details.length > 1) {
    orderForm.details.splice(index, 1)
  }
}

function handleOrderMaterialChange(index: number) {
  const line = orderForm.details[index]
  if (line) {
    line.materialId = Number(line.materialId)
  }
  const material = referenceData.value.materials.find(
    (item) => item.materialId === line?.materialId,
  )
  if (!line || !material) {
    return
  }
  if (!orderForm.supplierId && material.defaultSupplierId) {
    orderForm.supplierId = material.defaultSupplierId
  }
}

function getBuyerName(buyerId: number) {
  return (
    referenceData.value.buyers.find((buyer) => buyer.buyerId === buyerId)?.buyerName ??
    `采购员 #${buyerId}`
  )
}

function getMaterialUnit(materialId: number) {
  return referenceData.value.materials.find((material) => material.materialId === materialId)?.unit
}

function getOverdueLabel(order: PurchaseOrderItem) {
  if (order.isOverdue) {
    return `已逾期 ${order.overdueDays} 天`
  }
  return '正常'
}

function validateOrderSelections() {
  if (!Number.isInteger(orderForm.buyerId) || orderForm.buyerId <= 0) {
    ElMessage.warning('请选择有效采购员')
    return false
  }
  if (
    orderForm.details.some(
      (item) => item.materialId <= 0 || item.quantity <= 0 || item.unitPrice < 0,
    )
  ) {
    ElMessage.warning('请完整填写采购物料、数量和单价')
    return false
  }
  if (!Number.isInteger(orderForm.supplierId) || orderForm.supplierId <= 0) {
    ElMessage.warning('请选择有效供应商')
    return false
  }
  const selectedMaterialIds = orderForm.details.map((item) => item.materialId)
  if (new Set(selectedMaterialIds).size !== selectedMaterialIds.length) {
    ElMessage.warning('同一物料不能重复添加')
    return false
  }
  return true
}

async function saveOrderForm(buyerId: number) {
  const payload = {
    buyerId,
    details: orderForm.details.map((item) => ({ ...item })),
    expectedDate: orderForm.expectedDate,
    supplierId: orderForm.supplierId,
  }
  await purchaseService.createOrder(payload)
  ElMessage.success('采购订单草稿已创建')
}

async function submitOrderForm() {
  const { buyerId } = orderForm
  const valid = await orderFormRef.value?.validate().catch(() => false)
  if (!valid || orderSubmitting.value || !validateOrderSelections()) {
    return
  }
  orderSubmitting.value = true
  try {
    await saveOrderForm(buyerId)
    orderDialogOpen.value = false
    orderQuery.page = 1
    await Promise.all([loadOrders(), loadSummary(), loadReferenceData()])
    await refreshDetailIfOpen()
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '保存采购订单失败'))
  } finally {
    orderSubmitting.value = false
  }
}

async function viewOrder(orderId: number) {
  const currentRequestId = ++detailRequestId
  detailDrawerOpen.value = true
  detailLoading.value = true
  selectedOrder.value = undefined
  detailReceipts.value = []
  detailReminders.value = []
  try {
    const [result, receiptResult, reminderResult] = await Promise.all([
      purchaseService.getOrder(orderId),
      purchaseService.listReceipts({ orderId, page: 1, pageSize: 50 }),
      purchaseService.listReminders({ orderId, page: 1, pageSize: 50 }),
    ])
    if (alive && currentRequestId === detailRequestId) {
      selectedOrder.value = result
      detailReceipts.value = receiptResult.items
      detailReminders.value = reminderResult.items
    }
  } catch (error) {
    if (alive && currentRequestId === detailRequestId) {
      ElMessage.error(getErrorMessage(error, '采购订单详情加载失败'))
      detailDrawerOpen.value = false
    }
  } finally {
    if (alive && currentRequestId === detailRequestId) {
      detailLoading.value = false
    }
  }
}

async function refreshDetailIfOpen() {
  if (detailDrawerOpen.value && selectedOrder.value) {
    await viewOrder(selectedOrder.value.orderId)
  }
}

async function changeOrderStatus(item: PurchaseOrderItem, action: 'cancel' | 'submit') {
  if (!operatorId.value) {
    ElMessage.error('当前会话缺少操作人信息，请重新登录')
    return
  }
  if (actingOrderId.value) {
    return
  }
  const config = orderActionConfig[action]
  actingOrderId.value = item.orderId
  try {
    await ElMessageBox.confirm(config.message(item.orderId), config.title, {
      confirmButtonText: config.confirmButtonText,
      type: config.type,
    })
    await config.request(item.orderId, operatorId.value)
    ElMessage.success(config.successMessage)
    await Promise.all([loadOrders(), loadSummary(), loadReferenceData()])
    await refreshDetailIfOpen()
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') {
      ElMessage.error(getErrorMessage(error, config.errorMessage))
    }
  } finally {
    actingOrderId.value = undefined
  }
}

function openReceiptDialog(order?: PurchaseOrderItem) {
  Object.assign(receiptForm, {
    materialId: order?.details.find((line) => line.receivedQty < line.quantity)?.materialId ?? 0,
    orderId: order?.orderId ?? 0,
    quantity: 1,
    receiveDate: new Date().toISOString().slice(0, 10),
  })
  receiptDialogOpen.value = true
}

function handleReceiptOrderChange(orderId: number) {
  const order = receivableOrders.value.find((item) => item.orderId === orderId)
  receiptForm.materialId =
    order?.details.find((line) => line.receivedQty < line.quantity)?.materialId ?? 0
  receiptForm.quantity = 1
}

async function submitReceipt() {
  const valid = await receiptFormRef.value?.validate().catch(() => false)
  if (!valid || receiptSubmitting.value) {
    return
  }
  receiptSubmitting.value = true
  try {
    await purchaseService.addReceipt({ ...receiptForm })
    ElMessage.success('采购收货记录已登记')
    receiptDialogOpen.value = false
    let loadList = loadOrders
    if (activeTab.value === 'receipts') {
      loadList = loadReceipts
    }
    await Promise.all([loadSummary(), loadList(), loadReferenceData(), loadReminders()])
    await refreshDetailIfOpen()
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '采购收货登记失败'))
  } finally {
    receiptSubmitting.value = false
  }
}

async function generateReminders() {
  if (generatingReminders.value) {
    return
  }
  generatingReminders.value = true
  try {
    const result = await purchaseService.generateReminders(reminderQuery.orderId)
    let message = '没有需要新增的逾期提醒'
    if (result.generatedCount > 0) {
      message = `已生成 ${result.generatedCount} 条逾期提醒`
    }
    ElMessage.success(message)
    await Promise.all([loadReminders(), loadSummary()])
    await refreshDetailIfOpen()
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '生成逾期提醒失败'))
  } finally {
    generatingReminders.value = false
  }
}

function openReminderDialog(item: PurchaseReminderItem) {
  reminderTarget.value = item
  let status: 'received' | 'urged' = 'received'
  if (item.status === 'pending_urge') {
    status = 'urged'
  }
  Object.assign(reminderForm, {
    remark: item.remark ?? '',
    status,
  })
  reminderDialogOpen.value = true
}

async function submitReminder() {
  if (!reminderTarget.value || reminderSubmitting.value) {
    return
  }
  reminderSubmitting.value = true
  try {
    await purchaseService.handleReminder(
      reminderTarget.value.reminderId,
      reminderForm.status,
      reminderForm.remark,
    )
    ElMessage.success('逾期提醒状态已更新')
    reminderDialogOpen.value = false
    await Promise.all([loadReminders(), loadSummary()])
    await refreshDetailIfOpen()
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '逾期提醒处理失败'))
  } finally {
    reminderSubmitting.value = false
  }
}

watch(activeTab, loadActiveTab)
onMounted(() => {
  void loadSummary()
  void loadOrders()
  void loadReferenceData()
})
onBeforeUnmount(() => {
  alive = false
  detailRequestId += 1
  orderRequestId += 1
  receiptRequestId += 1
  reminderRequestId += 1
  referenceRequestId += 1
  summaryRequestId += 1
})
</script>

<template>
  <PageContainer>
    <PageHeader
      title="采购管理"
      description="统一管理采购订单、分批收货和逾期催交，持续跟踪供应进度。"
    >
      <template #actions>
        <el-button :icon="Refresh" :loading="refreshing" @click="refreshAll">刷新数据</el-button>
        <el-button :icon="CirclePlus" type="primary" @click="openOrderDialog()"
          >新建采购订单</el-button
        >
      </template>
    </PageHeader>

    <section class="purchase-statistics" aria-label="采购统计">
      <el-card
        v-for="statistic in statistics"
        :key="statistic.label"
        v-loading="summaryLoading"
        class="purchase-statistic"
        :class="`purchase-statistic--${statistic.tone}`"
        shadow="never"
        ><span>{{ statistic.label }}</span
        ><strong>{{ formatNumber(statistic.value) }}</strong></el-card
      >
    </section>

    <el-alert
      v-if="summaryError"
      class="summary-error"
      :closable="false"
      :title="summaryError"
      type="error"
      show-icon
      ><template #default
        ><el-button link type="primary" @click="loadSummary">重新加载概览</el-button></template
      ></el-alert
    >

    <el-alert
      v-if="referenceError"
      class="summary-error"
      :closable="false"
      :title="referenceError"
      type="warning"
      show-icon
      ><template #default
        ><el-button link type="primary" @click="loadReferenceData"
          >重新加载关联选项</el-button
        ></template
      ></el-alert
    >

    <el-card class="purchase-card" shadow="never">
      <el-tabs v-model="activeTab">
        <el-tab-pane name="orders">
          <template #label
            ><span class="tab-label"
              ><el-icon><Document /></el-icon>采购订单</span
            ></template
          >
          <div class="toolbar">
            <div class="filters">
              <el-input-number
                v-model="orderQuery.orderId"
                clearable
                :min="1"
                placeholder="订单编号"
                :precision="0"
              />
              <el-select
                v-model="orderQuery.supplierId"
                clearable
                filterable
                :loading="referenceLoading"
                placeholder="供应商"
                ><el-option
                  v-for="supplier in referenceData.suppliers"
                  :key="supplier.supplierId"
                  :label="supplier.supplierName"
                  :value="supplier.supplierId" /></el-select
              ><el-select
                v-model="orderQuery.materialId"
                clearable
                filterable
                :loading="referenceLoading"
                placeholder="物料"
                ><el-option
                  v-for="material in referenceData.materials"
                  :key="material.materialId"
                  :label="material.materialName"
                  :value="material.materialId" /></el-select
              ><el-select v-model="orderQuery.status" clearable placeholder="订单状态"
                ><el-option
                  v-for="(label, value) in orderStatusLabels"
                  :key="value"
                  :label="label"
                  :value="value" /></el-select
              ><el-select
                v-model="orderQuery.buyerId"
                clearable
                filterable
                :loading="referenceLoading"
                placeholder="采购员"
                ><el-option
                  v-for="buyer in referenceData.buyers"
                  :key="buyer.buyerId"
                  :label="buyer.buyerName"
                  :value="buyer.buyerId" /></el-select
              ><el-date-picker
                v-model="orderDateRange"
                end-placeholder="结束日期"
                range-separator="至"
                start-placeholder="下单开始"
                type="daterange"
                value-format="YYYY-MM-DD"
              /><el-date-picker
                v-model="expectedDateRange"
                end-placeholder="交期结束"
                range-separator="至"
                start-placeholder="交期开始"
                type="daterange"
                value-format="YYYY-MM-DD"
              /><el-button :icon="Search" type="primary" @click="searchOrders">查询</el-button
              ><el-button @click="resetOrderQuery">重置</el-button>
            </div>
          </div>
          <el-alert
            v-if="orderError"
            class="list-error"
            :closable="false"
            :title="orderError"
            type="error"
            ><template #default
              ><el-button link type="primary" @click="loadOrders">重试</el-button></template
            ></el-alert
          >
          <div v-loading="orderLoading" class="table-area">
            <EmptyState
              v-if="!orderLoading && !orderError && !orderItems.length"
              description="当前筛选条件下没有采购订单。"
            />
            <el-table v-else :data="orderItems" stripe>
              <el-table-column label="订单" min-width="100"
                ><template #default="{ row }"
                  ><el-button link type="primary" @click="viewOrder(row.orderId)"
                    >#{{ row.orderId }}</el-button
                  ></template
                ></el-table-column
              >
              <el-table-column label="供应商" min-width="190"
                ><template #default="{ row }"
                  ><div class="primary-cell">
                    <strong>{{ row.supplier.supplierName }}</strong
                    ><small>ID {{ row.supplier.supplierId }}</small>
                  </div></template
                ></el-table-column
              >
              <el-table-column label="下单 / 交期" min-width="190"
                ><template #default="{ row }"
                  ><div class="date-cell">
                    <span>{{ row.orderDate }}</span
                    ><span :class="{ overdue: row.isOverdue }"
                      >{{ row.expectedDate
                      }}<small v-if="row.isOverdue">逾期 {{ row.overdueDays }} 天</small></span
                    >
                  </div></template
                ></el-table-column
              >
              <el-table-column label="总金额" min-width="130"
                ><template #default="{ row }">{{
                  formatAmount(row.totalAmount)
                }}</template></el-table-column
              >
              <el-table-column label="采购员" min-width="120"
                ><template #default="{ row }">{{
                  getBuyerName(row.buyerId)
                }}</template></el-table-column
              >
              <el-table-column label="到货进度" min-width="150"
                ><template #default="{ row }"
                  ><el-progress
                    :percentage="Math.round(row.receiveProgress * 100)"
                    :stroke-width="8" /></template
              ></el-table-column>
              <el-table-column label="状态" min-width="110"
                ><template #default="{ row }"
                  ><StatusTag :labels="orderStatusLabels" :value="row.status" /></template
              ></el-table-column>
              <el-table-column fixed="right" label="操作" min-width="250"
                ><template #default="{ row }"
                  ><el-button link type="primary" @click="viewOrder(row.orderId)">详情</el-button
                  ><el-button
                    v-if="row.status === 'draft'"
                    :loading="actingOrderId === row.orderId"
                    link
                    type="primary"
                    @click="changeOrderStatus(row, 'submit')"
                    >提交</el-button
                  ><el-button
                    v-if="['submitted', 'partial_received'].includes(row.status)"
                    link
                    type="success"
                    @click="openReceiptDialog(row)"
                    >收货</el-button
                  ><el-button
                    v-if="['draft', 'submitted'].includes(row.status)"
                    :loading="actingOrderId === row.orderId"
                    link
                    type="danger"
                    @click="changeOrderStatus(row, 'cancel')"
                    >取消</el-button
                  ></template
                ></el-table-column
              >
            </el-table>
          </div>
          <el-pagination
            v-if="orderTotal"
            v-model:current-page="orderQuery.page"
            v-model:page-size="orderQuery.pageSize"
            :page-sizes="[10, 20, 50]"
            background
            layout="total, sizes, prev, pager, next"
            :total="orderTotal"
            @change="loadOrders"
          />
        </el-tab-pane>

        <el-tab-pane name="receipts">
          <template #label
            ><span class="tab-label"
              ><el-icon><Van /></el-icon>收货记录</span
            ></template
          >
          <div class="toolbar">
            <div class="filters">
              <el-select v-model="receiptQuery.orderId" clearable filterable placeholder="采购订单"
                ><el-option
                  v-for="order in referenceData.orders"
                  :key="order.orderId"
                  :label="`#${order.orderId} · ${order.supplier.supplierName}`"
                  :value="order.orderId" /></el-select
              ><el-select v-model="receiptQuery.materialId" clearable filterable placeholder="物料"
                ><el-option
                  v-for="material in referenceData.materials"
                  :key="material.materialId"
                  :label="material.materialName"
                  :value="material.materialId" /></el-select
              ><el-button :icon="Search" type="primary" @click="searchReceipts">查询</el-button
              ><el-button @click="resetReceiptQuery">重置</el-button>
            </div>
            <el-button :icon="Plus" type="primary" @click="openReceiptDialog()">登记收货</el-button>
          </div>
          <el-alert
            v-if="receiptError"
            class="list-error"
            :closable="false"
            :title="receiptError"
            type="error"
            ><template #default
              ><el-button link type="primary" @click="loadReceipts">重试</el-button></template
            ></el-alert
          >
          <div v-loading="receiptLoading" class="table-area">
            <EmptyState
              v-if="!receiptLoading && !receiptError && !receiptItems.length"
              description="当前筛选条件下没有采购收货记录。"
            />
            <el-table v-else :data="receiptItems" stripe
              ><el-table-column label="收货 ID" min-width="100" prop="receiveId" /><el-table-column
                label="采购订单"
                min-width="110"
                ><template #default="{ row }"
                  ><el-button link type="primary" @click="viewOrder(row.orderId)"
                    >#{{ row.orderId }}</el-button
                  ></template
                ></el-table-column
              ><el-table-column label="物料" min-width="210"
                ><template #default="{ row }"
                  ><div class="primary-cell">
                    <strong>{{ row.materialName || `物料 #${row.materialId}` }}</strong
                    ><small>ID {{ row.materialId }}</small>
                  </div></template
                ></el-table-column
              ><el-table-column label="本次收货数量" min-width="140"
                ><template #default="{ row }"
                  ><strong>{{ formatNumber(row.quantity) }}</strong></template
                ></el-table-column
              ><el-table-column label="收货日期" min-width="130" prop="receiveDate"
            /></el-table>
          </div>
          <el-pagination
            v-if="receiptTotal"
            v-model:current-page="receiptQuery.page"
            v-model:page-size="receiptQuery.pageSize"
            :page-sizes="[10, 20, 50]"
            background
            layout="total, sizes, prev, pager, next"
            :total="receiptTotal"
            @change="loadReceipts"
          />
        </el-tab-pane>

        <el-tab-pane name="reminders">
          <template #label
            ><span class="tab-label"
              ><el-icon><Bell /></el-icon>逾期催交</span
            ></template
          >
          <div class="toolbar">
            <div class="filters">
              <el-select v-model="reminderQuery.orderId" clearable filterable placeholder="采购订单"
                ><el-option
                  v-for="order in referenceData.orders"
                  :key="order.orderId"
                  :label="`#${order.orderId} · ${order.supplier.supplierName}`"
                  :value="order.orderId" /></el-select
              ><el-select v-model="reminderQuery.status" clearable placeholder="处理状态"
                ><el-option
                  v-for="(label, value) in reminderStatusLabels"
                  :key="value"
                  :label="label"
                  :value="value" /></el-select
              ><el-button :icon="Search" type="primary" @click="searchReminders">查询</el-button
              ><el-button @click="resetReminderQuery">重置</el-button>
            </div>
            <el-button
              :icon="Bell"
              :loading="generatingReminders"
              type="primary"
              @click="generateReminders"
              >生成逾期提醒</el-button
            >
          </div>
          <el-alert
            v-if="reminderError"
            class="list-error"
            :closable="false"
            :title="reminderError"
            type="error"
            ><template #default
              ><el-button link type="primary" @click="loadReminders">重试</el-button></template
            ></el-alert
          >
          <div v-loading="reminderLoading" class="table-area">
            <EmptyState
              v-if="!reminderLoading && !reminderError && !reminderItems.length"
              description="当前筛选条件下没有采购逾期提醒。"
            />
            <el-table v-else :data="reminderItems" stripe
              ><el-table-column label="提醒 ID" min-width="100" prop="reminderId" /><el-table-column
                label="采购订单"
                min-width="110"
                ><template #default="{ row }"
                  ><el-button link type="primary" @click="viewOrder(row.orderId)"
                    >#{{ row.orderId }}</el-button
                  ></template
                ></el-table-column
              ><el-table-column
                label="预计交期"
                min-width="120"
                prop="expectedDate"
              /><el-table-column label="逾期天数" min-width="100"
                ><template #default="{ row }"
                  ><strong class="overdue">{{ row.overdueDays }} 天</strong></template
                ></el-table-column
              ><el-table-column label="提醒时间" min-width="175"
                ><template #default="{ row }">{{
                  formatDateTime(row.remindTime)
                }}</template></el-table-column
              ><el-table-column
                label="备注"
                min-width="210"
                prop="remark"
                show-overflow-tooltip
              /><el-table-column label="状态" min-width="110"
                ><template #default="{ row }"
                  ><StatusTag
                    :labels="reminderStatusLabels"
                    :value="row.status" /></template></el-table-column
              ><el-table-column fixed="right" label="操作" min-width="100"
                ><template #default="{ row }"
                  ><el-button
                    v-if="row.status !== 'received'"
                    link
                    type="primary"
                    @click="openReminderDialog(row)"
                    >处理</el-button
                  ><span v-else>-</span></template
                ></el-table-column
              ></el-table
            >
          </div>
          <el-pagination
            v-if="reminderTotal"
            v-model:current-page="reminderQuery.page"
            v-model:page-size="reminderQuery.pageSize"
            :page-sizes="[10, 20, 50]"
            background
            layout="total, sizes, prev, pager, next"
            :total="reminderTotal"
            @change="loadReminders"
          />
        </el-tab-pane>
      </el-tabs>
    </el-card>

    <el-dialog
      v-model="orderDialogOpen"
      title="新建采购订单草稿"
      width="min(96vw, 780px)"
      @closed="orderFormRef?.resetFields()"
      ><el-form ref="orderFormRef" :model="orderForm" :rules="orderRules" label-width="100px"
        ><div class="order-form-grid">
          <el-form-item label="采购员" prop="buyerId">
            <el-select
              v-model="orderForm.buyerId"
              allow-create
              default-first-option
              filterable
              :loading="referenceLoading"
              placeholder="选择采购员或输入编号"
              @change="orderForm.buyerId = Number($event)"
            >
              <el-option
                v-for="buyer in referenceData.buyers"
                :key="buyer.buyerId"
                :label="buyer.buyerName"
                :value="buyer.buyerId"
              />
            </el-select>
          </el-form-item>
          <el-form-item label="供应商" prop="supplierId">
            <el-select
              v-model="orderForm.supplierId"
              allow-create
              default-first-option
              filterable
              :loading="referenceLoading"
              placeholder="选择供应商或输入编号"
              @change="orderForm.supplierId = Number($event)"
            >
              <el-option
                v-for="supplier in activeSuppliers"
                :key="supplier.supplierId"
                :label="`${supplier.supplierName} · ${supplier.contactPerson || '暂无联系人'}`"
                :value="supplier.supplierId"
              />
            </el-select>
          </el-form-item>
          <el-form-item label="预计交期" prop="expectedDate">
            <el-date-picker
              v-model="orderForm.expectedDate"
              placeholder="选择日期"
              type="date"
              value-format="YYYY-MM-DD"
            />
          </el-form-item>
        </div>
        <el-form-item label="采购明细"
          ><div class="order-lines">
            <div class="order-line order-line--header">
              <span>采购物料</span><span>采购数量</span><span>含税单价</span><span>行金额</span
              ><span></span>
            </div>
            <div v-for="(line, index) in orderForm.details" :key="index" class="order-line">
              <el-select
                v-model="line.materialId"
                allow-create
                default-first-option
                filterable
                placeholder="选择物料或输入编号"
                @change="handleOrderMaterialChange(index)"
                ><el-option
                  v-for="material in referenceData.materials"
                  :key="material.materialId"
                  :disabled="
                    orderForm.details.some(
                      (item, itemIndex) =>
                        itemIndex !== index && item.materialId === material.materialId,
                    )
                  "
                  :label="`${material.materialName}${material.unit ? ` (${material.unit})` : ''}`"
                  :value="material.materialId" /></el-select
              ><el-input-number
                v-model="line.quantity"
                :min="0.01"
                :precision="2"
              /><el-input-number v-model="line.unitPrice" :min="0" :precision="2" /><strong
                class="line-amount"
                >{{ formatAmount(line.quantity * line.unitPrice) }}</strong
              ><el-button
                :disabled="orderForm.details.length === 1"
                text
                type="danger"
                @click="removeOrderLine(index)"
                >移除</el-button
              >
            </div>
            <el-button :icon="Plus" text type="primary" @click="addOrderLine">添加明细</el-button>
          </div></el-form-item
        >
        <div class="amount-summary">
          预计总金额 <strong>{{ formatAmount(orderTotalAmount) }}</strong>
        </div></el-form
      ><template #footer
        ><el-button @click="orderDialogOpen = false">取消</el-button
        ><el-button :loading="orderSubmitting" type="primary" @click="submitOrderForm"
          >创建草稿</el-button
        ></template
      ></el-dialog
    >

    <el-dialog
      v-model="receiptDialogOpen"
      title="登记采购收货"
      width="min(92vw, 480px)"
      @closed="receiptFormRef?.resetFields()"
      ><el-form ref="receiptFormRef" :model="receiptForm" :rules="receiptRules" label-width="105px"
        ><el-form-item label="采购订单" prop="orderId"
          ><el-select
            v-model="receiptForm.orderId"
            filterable
            placeholder="选择待收货订单"
            style="width: 100%"
            @change="handleReceiptOrderChange"
            ><el-option
              v-for="order in receivableOrders"
              :key="order.orderId"
              :label="`#${order.orderId} · ${order.supplier.supplierName}`"
              :value="order.orderId" /></el-select></el-form-item
        ><el-form-item label="采购物料" prop="materialId"
          ><el-select
            v-model="receiptForm.materialId"
            filterable
            placeholder="选择未收完物料"
            style="width: 100%"
            ><el-option
              v-for="line in receiptMaterialOptions"
              :key="line.materialId"
              :label="`${line.materialName || `物料 #${line.materialId}`} · 剩余 ${formatNumber(
                line.quantity - line.receivedQty,
              )}`"
              :value="line.materialId" /></el-select></el-form-item
        ><el-form-item label="收货数量" prop="quantity"
          ><el-input-number
            v-model="receiptForm.quantity"
            :max="receiptRemainingQty"
            :min="0.01"
            :precision="2"
            style="width: 100%" /></el-form-item
        ><el-form-item label="收货日期" prop="receiveDate"
          ><el-date-picker
            v-model="receiptForm.receiveDate"
            type="date"
            value-format="YYYY-MM-DD"
            style="width: 100%" /></el-form-item></el-form
      ><template #footer
        ><el-button @click="receiptDialogOpen = false">取消</el-button
        ><el-button :loading="receiptSubmitting" type="primary" @click="submitReceipt"
          >确认收货</el-button
        ></template
      ></el-dialog
    >

    <el-dialog v-model="reminderDialogOpen" title="处理逾期提醒" width="min(92vw, 480px)"
      ><el-form label-width="90px"
        ><el-form-item label="处理结果"
          ><el-segmented
            v-model="reminderForm.status"
            :options="reminderStatusOptions" /></el-form-item
        ><el-form-item label="处理备注"
          ><el-input
            v-model="reminderForm.remark"
            maxlength="300"
            :rows="4"
            show-word-limit
            type="textarea" /></el-form-item></el-form
      ><template #footer
        ><el-button @click="reminderDialogOpen = false">取消</el-button
        ><el-button :loading="reminderSubmitting" type="primary" @click="submitReminder"
          >确认处理</el-button
        ></template
      ></el-dialog
    >

    <el-drawer v-model="detailDrawerOpen" size="min(94vw, 720px)" title="采购订单详情"
      ><div v-loading="detailLoading" class="detail-area">
        <template v-if="selectedOrder"
          ><div class="detail-summary">
            <div>
              <span>订单编号</span><strong>#{{ selectedOrder.orderId }}</strong>
            </div>
            <div>
              <span>供应商</span><strong>{{ selectedOrder.supplier.supplierName }}</strong>
            </div>
            <div>
              <span>供应商联系人</span
              ><strong>{{ selectedOrder.supplier.contactPerson || '-' }}</strong>
            </div>
            <div>
              <span>采购员</span><strong>{{ getBuyerName(selectedOrder.buyerId) }}</strong>
            </div>
            <div>
              <span>订单状态</span
              ><StatusTag :labels="orderStatusLabels" :value="selectedOrder.status" />
            </div>
            <div>
              <span>订单总额</span><strong>{{ formatAmount(selectedOrder.totalAmount) }}</strong>
            </div>
            <div>
              <span>下单日期</span><strong>{{ selectedOrder.orderDate }}</strong>
            </div>
            <div>
              <span>预计交期</span
              ><strong :class="{ overdue: selectedOrder.isOverdue }">{{
                selectedOrder.expectedDate
              }}</strong>
            </div>
            <div>
              <span>逾期状态</span
              ><strong :class="{ overdue: selectedOrder.isOverdue }">{{
                getOverdueLabel(selectedOrder)
              }}</strong>
            </div>
            <div>
              <span>收货进度</span
              ><el-progress :percentage="Math.round(selectedOrder.receiveProgress * 100)" />
            </div>
            <div>
              <span>实际完成日期</span><strong>{{ selectedOrder.actualDate || '-' }}</strong>
            </div>
          </div>
          <el-divider content-position="left">采购明细</el-divider
          ><el-table :data="selectedOrder.details" stripe
            ><el-table-column label="物料" min-width="180"
              ><template #default="{ row }"
                ><div class="primary-cell">
                  <strong>{{ row.materialName || `物料 #${row.materialId}` }}</strong
                  ><small
                    >ID {{ row.materialId }} · 单位
                    {{ getMaterialUnit(row.materialId) || '-' }}</small
                  >
                </div></template
              ></el-table-column
            ><el-table-column label="采购 / 已收" min-width="130"
              ><template #default="{ row }"
                >{{ formatNumber(row.quantity) }} / {{ formatNumber(row.receivedQty) }}</template
              ></el-table-column
            ><el-table-column label="单价" min-width="110"
              ><template #default="{ row }">{{
                formatAmount(row.unitPrice)
              }}</template></el-table-column
            ><el-table-column label="金额" min-width="120"
              ><template #default="{ row }">{{
                formatAmount(row.lineAmount)
              }}</template></el-table-column
            ><el-table-column label="进度" min-width="120"
              ><template #default="{ row }"
                ><el-progress
                  :percentage="Math.round(row.receiveProgress * 100)"
                  :stroke-width="7" /></template></el-table-column></el-table
          ><el-divider content-position="left">收货记录</el-divider
          ><EmptyState
            v-if="!detailReceipts.length"
            description="该采购订单暂无收货记录。" /><el-table v-else :data="detailReceipts" stripe
            ><el-table-column label="物料" min-width="180"
              ><template #default="{ row }">{{
                row.materialName || `物料 #${row.materialId}`
              }}</template></el-table-column
            ><el-table-column label="本次数量" min-width="110" prop="quantity" /><el-table-column
              label="收货日期"
              min-width="130"
              prop="receiveDate" /></el-table
          ><el-divider content-position="left">催交信息</el-divider
          ><EmptyState
            v-if="!detailReminders.length"
            description="该采购订单暂无催交记录。" /><el-table v-else :data="detailReminders" stripe
            ><el-table-column label="提醒时间" min-width="160"
              ><template #default="{ row }">{{
                formatDateTime(row.remindTime)
              }}</template></el-table-column
            ><el-table-column label="状态" min-width="110"
              ><template #default="{ row }"
                ><StatusTag
                  :labels="reminderStatusLabels"
                  :value="row.status" /></template></el-table-column
            ><el-table-column label="处理备注" min-width="180" prop="remark" /></el-table
        ></template></div
    ></el-drawer>
  </PageContainer>
</template>

<style scoped>
.purchase-statistics {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 14px;
  margin-bottom: 16px;
}
.summary-error {
  margin-bottom: 16px;
}
.purchase-statistic {
  min-width: 0;
  border-top-width: 3px;
}
.purchase-statistic--blue {
  border-top-color: var(--primary-color);
  background: var(--app-background);
}
.purchase-statistic--pink {
  border-top-color: var(--border-color);
  background: var(--card-background);
}
.purchase-statistic span {
  color: var(--el-text-color-secondary);
}
.purchase-statistic strong {
  display: block;
  margin-top: 10px;
  font-size: 27px;
}
.purchase-card {
  min-width: 0;
  overflow: hidden;
  border-top: 3px solid var(--border-color);
}
.tab-label,
.toolbar,
.filters {
  display: flex;
  align-items: center;
  gap: 7px;
}
.toolbar {
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 16px;
}
.filters {
  flex-wrap: wrap;
  min-width: 0;
}
.filters :deep(.el-input-number),
.filters :deep(.el-select) {
  width: 140px;
}
.filters :deep(.el-date-editor) {
  width: 230px;
}
.list-error {
  margin-bottom: 12px;
}
.table-area {
  width: 100%;
  min-width: 0;
  min-height: 260px;
}
:deep(.el-card__body),
:deep(.el-tabs__content),
:deep(.el-tab-pane) {
  min-width: 0;
}
.primary-cell,
.date-cell {
  display: grid;
  gap: 2px;
}
.primary-cell small,
.date-cell small {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
.date-cell > span:last-child {
  display: flex;
  gap: 5px;
}
.overdue {
  color: var(--primary-color);
}
:deep(.el-pagination) {
  justify-content: flex-end;
  margin-top: 16px;
}
.order-form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}
.order-form-grid :deep(.el-input-number),
.order-form-grid :deep(.el-date-editor),
.order-form-grid :deep(.el-select) {
  width: 100%;
}
.order-lines {
  display: grid;
  width: 100%;
  gap: 9px;
}
.order-line {
  display: grid;
  grid-template-columns:
    minmax(180px, 1.5fr) repeat(2, minmax(110px, 1fr)) minmax(100px, 0.8fr)
    52px;
  gap: 8px;
}
.order-line :deep(.el-input-number),
.order-line :deep(.el-select) {
  width: 100%;
}
.line-amount {
  align-self: center;
  color: var(--el-text-color-regular);
  text-align: right;
}
.order-line--header {
  color: var(--el-text-color-secondary);
  font-size: 12px;
  padding: 0 10px;
}
.amount-summary {
  border-radius: 6px;
  background: var(--card-background);
  padding: 12px 16px;
  text-align: right;
}
.amount-summary strong {
  margin-left: 10px;
  color: var(--primary-color);
  font-size: 18px;
}
.detail-area {
  min-height: 240px;
}
.detail-summary {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
}
.detail-summary > div {
  display: grid;
  gap: 5px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  background: var(--app-background);
  padding: 12px;
}
.detail-summary span {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
@media (max-width: 960px) {
  .purchase-statistics {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .detail-summary {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
@media (max-width: 720px) {
  .toolbar {
    align-items: stretch;
    flex-direction: column;
  }
  .filters > * {
    flex: 1 1 130px;
  }
  .filters :deep(.el-date-editor) {
    width: 100%;
  }
  .order-form-grid,
  .detail-summary {
    grid-template-columns: 1fr;
  }
  .order-line {
    grid-template-columns: 1fr;
  }
  .order-line--header {
    display: none;
  }
}
</style>
