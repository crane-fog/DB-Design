<script setup lang="ts">
import { Calendar, EditPen, Plus, Refresh, TrendCharts, View } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import {
  type ProductionOrderFormData,
  type ProductionOrderItem,
  type ProductionOrderStatus,
  type ProductionProgressReportFormData,
  type ProductionScheduleFormData,
  type ProductionStageItem,
  productionService,
} from '@/services/ProductionService'
import { computed, onMounted, reactive, ref } from 'vue'
import { PERMISSIONS } from '@/constants/permissions'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import type { PageResult } from '@/services/pagination'
import StatusTag from '@/components/common/StatusTag.vue'
import { formatDateTime } from '@/utils/format'
import { getErrorMessage } from '@/utils/error'
import { parsePositiveInt } from '@/utils/parse'
import { productionOrderStatusLabels as statusLabels } from '@/constants/status'
import { useAuthStore } from '@/stores/auth'

const pageSize = 10
const auth = useAuthStore()
const filters = reactive({ materialId: '', planEndEnd: '', planEndStart: '', status: '' })
const page = ref(1)
const loading = ref(false)
const error = ref('')
const result = ref<PageResult<ProductionOrderItem>>({ items: [], page: 1, pageSize, total: 0 })

const canManage = computed(() => auth.hasPermission(PERMISSIONS.production.orders))

const orderDialogVisible = ref(false)
const orderDialogMode = ref<'create' | 'edit'>('create')
const orderFormRef = ref<FormInstance>()
const editingOrderId = ref<number>()
const submitting = ref(false)
const actionSubmitting = ref(false)
const progressDialogVisible = ref(false)
const progressFormRef = ref<FormInstance>()
const progressSubmitting = ref(false)
const progressTarget = ref<ProductionOrderItem>()
const scheduleDialogVisible = ref(false)
const scheduleFormRef = ref<FormInstance>()
const scheduleSubmitting = ref(false)
const scheduleTarget = ref<ProductionOrderItem>()
const lineLoading = ref(false)
const lineOptions = ref<Awaited<ReturnType<typeof productionService.listLines>>['items']>([])

const detailVisible = ref(false)
const detailLoading = ref(false)
const detailError = ref('')
const detail = ref<ProductionOrderItem>()
const detailStages = ref<ProductionStageItem[]>([])

const orderForm = reactive<ProductionOrderFormData>({
  materialId: 0,
  planEnd: '',
  planQty: 1,
  planStart: '',
  versionId: 0,
})
const progressForm = reactive<ProductionProgressReportFormData>({
  completedQty: 1,
  orderId: 0,
  remark: '',
  reportedAt: '',
})
const scheduleForm = reactive<ProductionScheduleFormData>({
  lineId: 0,
  orderId: 0,
  plannedEnd: '',
  plannedStart: '',
})

const orderDialogTitle = computed(() => {
  if (orderDialogMode.value === 'create') {
    return '新增生产订单'
  }
  return '修改生产订单计划'
})

const orderRules: FormRules<ProductionOrderFormData> = {
  materialId: [
    { message: '请输入产品物料 ID', required: true, trigger: 'blur', type: 'number' },
    { message: '物料 ID 必须大于 0', min: 1, trigger: 'blur', type: 'number' },
  ],
  planEnd: [{ message: '请选择计划完工时间', required: true, trigger: 'change' }],
  planQty: [
    { message: '请输入计划数量', required: true, trigger: 'blur', type: 'number' },
    { message: '计划数量必须大于 0', min: 1, trigger: 'blur', type: 'number' },
  ],
  planStart: [{ message: '请选择计划开工时间', required: true, trigger: 'change' }],
  versionId: [
    { message: '请输入 BOM 版本 ID', required: true, trigger: 'blur', type: 'number' },
    { message: 'BOM 版本 ID 必须大于 0', min: 1, trigger: 'blur', type: 'number' },
  ],
}
const progressRules: FormRules<ProductionProgressReportFormData> = {
  completedQty: [
    { message: '请输入本次完成数量', required: true, trigger: 'blur', type: 'number' },
    { message: '本次完成数量必须大于 0', min: 1, trigger: 'blur', type: 'number' },
  ],
  reportedAt: [{ message: '请选择上报时间', required: true, trigger: 'change' }],
}
const scheduleRules: FormRules<ProductionScheduleFormData> = {
  lineId: [{ message: '请选择可用生产线', required: true, trigger: 'change', type: 'number' }],
  plannedEnd: [{ message: '请选择计划结束时间', required: true, trigger: 'change' }],
  plannedStart: [{ message: '请选择计划开始时间', required: true, trigger: 'change' }],
}

function selectedStatus(): ProductionOrderStatus | undefined {
  const value = filters.status
  if (
    value === 'pending_review' ||
    value === 'pending_schedule' ||
    value === 'in_progress' ||
    value === 'completed' ||
    value === 'cancelled'
  ) {
    return value
  }
  return undefined
}

function parseMaterialId() {
  return parsePositiveInt(filters.materialId)
}

async function loadOrders(targetPage = page.value) {
  loading.value = true
  error.value = ''
  try {
    result.value = await productionService.listOrders({
      materialId: parseMaterialId(),
      page: targetPage,
      pageSize,
      planEndEnd: filters.planEndEnd || undefined,
      planEndStart: filters.planEndStart || undefined,
      status: selectedStatus(),
    })
    page.value = result.value.page
  } catch (requestError) {
    error.value = getErrorMessage(requestError, '生产订单列表加载失败')
  } finally {
    loading.value = false
  }
}

function resetFilters() {
  Object.assign(filters, { materialId: '', planEndEnd: '', planEndStart: '', status: '' })
  void loadOrders(1)
}

function resetOrderForm() {
  Object.assign(orderForm, {
    materialId: 0,
    planEnd: '',
    planQty: 1,
    planStart: '',
    versionId: 0,
  })
  editingOrderId.value = undefined
  orderFormRef.value?.clearValidate()
}

function openCreateDialog() {
  orderDialogMode.value = 'create'
  resetOrderForm()
  orderDialogVisible.value = true
}

function openEditDialog(order: ProductionOrderItem) {
  orderDialogMode.value = 'edit'
  Object.assign(orderForm, {
    materialId: order.materialId,
    planEnd: order.planEnd,
    planQty: order.planQty,
    planStart: order.planStart,
    versionId: order.versionId,
  })
  editingOrderId.value = order.orderId
  orderFormRef.value?.clearValidate()
  orderDialogVisible.value = true
}

async function submitOrderForm() {
  const valid = await orderFormRef.value?.validate().catch(() => false)
  if (!valid || submitting.value) {
    return
  }
  submitting.value = true
  try {
    if (orderDialogMode.value === 'create') {
      await productionService.createOrder({ ...orderForm })
      ElMessage.success('生产订单已创建')
    } else if (editingOrderId.value !== undefined) {
      await productionService.updateOrder(editingOrderId.value, { ...orderForm })
      ElMessage.success('生产订单计划已更新')
    }
    orderDialogVisible.value = false
    await loadOrders(page.value)
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '生产订单提交失败'))
  } finally {
    submitting.value = false
  }
}

async function openDetail(order: ProductionOrderItem) {
  detailVisible.value = true
  detailLoading.value = true
  detailError.value = ''
  detail.value = undefined
  detailStages.value = []
  try {
    const [orderDetail, stages] = await Promise.all([
      productionService.getOrder(order.orderId),
      productionService.getOrderStages(order.orderId),
    ])
    detail.value = orderDetail
    detailStages.value = stages
  } catch (requestError) {
    detailError.value = getErrorMessage(requestError, '生产订单详情加载失败')
  } finally {
    detailLoading.value = false
  }
}

async function loadLineOptions() {
  lineLoading.value = true
  try {
    const lineResult = await productionService.listLines({ page: 1, pageSize: 100 })
    lineOptions.value = lineResult.items.filter((line) => line.status !== 'fault')
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '可用生产线加载失败'))
  } finally {
    lineLoading.value = false
  }
}

function openProgressDialog(order: ProductionOrderItem) {
  progressTarget.value = order
  Object.assign(progressForm, {
    completedQty: 1,
    orderId: order.orderId,
    remark: '',
    reportedAt: new Date().toISOString().slice(0, 19).replace('T', ' '),
  })
  progressFormRef.value?.clearValidate()
  progressDialogVisible.value = true
}

async function submitProgress() {
  const valid = await progressFormRef.value?.validate().catch(() => false)
  if (!valid || progressSubmitting.value) {
    return
  }
  progressSubmitting.value = true
  try {
    await productionService.reportOrderProgress({ ...progressForm })
    ElMessage.success('生产进度已上报')
    progressDialogVisible.value = false
    await loadOrders(page.value)
    if (detail.value?.orderId === progressForm.orderId) {
      await openDetail(detail.value)
    }
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '生产进度上报失败'))
  } finally {
    progressSubmitting.value = false
  }
}

async function openScheduleDialog(order: ProductionOrderItem) {
  scheduleTarget.value = order
  Object.assign(scheduleForm, {
    lineId: order.schedule?.lineId ?? 0,
    orderId: order.orderId,
    plannedEnd: order.schedule?.plannedEnd ?? order.planEnd,
    plannedStart: order.schedule?.plannedStart ?? order.planStart,
  })
  scheduleFormRef.value?.clearValidate()
  scheduleDialogVisible.value = true
  if (!lineOptions.value.length) {
    await loadLineOptions()
  }
}

async function submitSchedule() {
  const valid = await scheduleFormRef.value?.validate().catch(() => false)
  if (!valid || scheduleSubmitting.value) {
    return
  }
  scheduleSubmitting.value = true
  try {
    await productionService.saveOrderSchedule({ ...scheduleForm })
    ElMessage.success('订单排产已保存')
    scheduleDialogVisible.value = false
    await loadOrders(page.value)
    if (detail.value?.orderId === scheduleForm.orderId) {
      await openDetail(detail.value)
    }
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '订单排产失败'))
  } finally {
    scheduleSubmitting.value = false
  }
}

async function approveOrder(order: ProductionOrderItem) {
  if (actionSubmitting.value) {
    return
  }
  try {
    const { value } = await ElMessageBox.prompt('请输入审核意见（可选）', '审核生产订单', {
      cancelButtonText: '拒绝',
      confirmButtonText: '通过',
      distinguishCancelAndClose: true,
      inputPlaceholder: '审核意见',
      inputType: 'textarea',
    }).then((response) => ({ approved: true, value: response.value }))
    actionSubmitting.value = true
    await productionService.approveOrder(order.orderId, true, value)
    ElMessage.success('生产订单已通过审核')
    await loadOrders(page.value)
  } catch (action) {
    if (action === 'cancel') {
      await rejectOrder(order)
      return
    }
    if (action !== 'close') {
      ElMessage.error(getErrorMessage(action, '审核生产订单失败'))
    }
  } finally {
    actionSubmitting.value = false
  }
}

async function rejectOrder(order: ProductionOrderItem) {
  try {
    const { value } = await ElMessageBox.prompt('请输入拒绝原因', '拒绝生产订单', {
      confirmButtonText: '确认拒绝',
      inputPlaceholder: '拒绝原因',
      inputType: 'textarea',
    })
    actionSubmitting.value = true
    await productionService.approveOrder(order.orderId, false, value)
    ElMessage.success('生产订单已拒绝')
    await loadOrders(page.value)
  } catch (requestError) {
    if (requestError !== 'cancel' && requestError !== 'close') {
      ElMessage.error(getErrorMessage(requestError, '拒绝生产订单失败'))
    }
  } finally {
    actionSubmitting.value = false
  }
}

async function startOrder(order: ProductionOrderItem) {
  if (actionSubmitting.value) {
    return
  }
  try {
    actionSubmitting.value = true
    await ElMessageBox.confirm(`确定要开工生产订单 #${order.orderId} 吗？`, '开工生产订单', {
      confirmButtonText: '确定开工',
      type: 'warning',
    })
    await productionService.startOrder(order.orderId)
    ElMessage.success('生产订单已开工')
    await loadOrders(page.value)
  } catch (requestError) {
    if (requestError !== 'cancel' && requestError !== 'close') {
      ElMessage.error(getErrorMessage(requestError, '开工生产订单失败'))
    }
  } finally {
    actionSubmitting.value = false
  }
}

async function finishOrder(order: ProductionOrderItem) {
  if (actionSubmitting.value) {
    return
  }
  try {
    const { value } = await ElMessageBox.prompt('请输入实际完工数量', '完工生产订单', {
      confirmButtonText: '确认完工',
      inputErrorMessage: '完工数量必须为大于 0 的整数',
      inputPattern: /^[1-9]\d*$/,
      inputValue: String(order.planQty),
    })
    actionSubmitting.value = true
    await productionService.finishOrder(order.orderId, Number(value))
    ElMessage.success('生产订单已完工')
    await loadOrders(page.value)
  } catch (requestError) {
    if (requestError !== 'cancel' && requestError !== 'close') {
      ElMessage.error(getErrorMessage(requestError, '完工生产订单失败'))
    }
  } finally {
    actionSubmitting.value = false
  }
}

async function cancelOrder(order: ProductionOrderItem) {
  if (actionSubmitting.value) {
    return
  }
  try {
    const { value } = await ElMessageBox.prompt('请输入取消原因（可选）', '取消生产订单', {
      confirmButtonText: '确认取消',
      inputPlaceholder: '取消原因',
      inputType: 'textarea',
    })
    actionSubmitting.value = true
    await productionService.cancelOrder(order.orderId, value)
    ElMessage.success('生产订单已取消')
    await loadOrders(page.value)
  } catch (requestError) {
    if (requestError !== 'cancel' && requestError !== 'close') {
      ElMessage.error(getErrorMessage(requestError, '取消生产订单失败'))
    }
  } finally {
    actionSubmitting.value = false
  }
}

function canReview(order: ProductionOrderItem) {
  return order.status === 'pending_review'
}
function canEdit(order: ProductionOrderItem) {
  return order.status === 'pending_review' || order.status === 'pending_schedule'
}
function canStart(order: ProductionOrderItem) {
  return order.status === 'pending_schedule' && Boolean(order.schedule)
}
function canFinish(order: ProductionOrderItem) {
  return order.status === 'in_progress'
}
function getStageStatus(stage: ProductionStageItem) {
  if (stage.status === 'completed') {
    return 'success'
  }
  if (stage.status === 'in_progress') {
    return 'process'
  }
  if (stage.status === 'paused') {
    return 'error'
  }
  return 'wait'
}
function canCancel(order: ProductionOrderItem) {
  return order.status !== 'completed' && order.status !== 'cancelled'
}

function progressPercentage(order: ProductionOrderItem) {
  if (order.planQty <= 0) {
    return 0
  }
  return Math.min(100, Math.round(((order.finishedQty ?? 0) / order.planQty) * 100))
}

onMounted(() => {
  void loadOrders()
  void loadLineOptions()
})
</script>

<template>
  <PageContainer>
    <PageHeader title="生产订单" description="按状态管理生产订单的审核、开工、完工与取消流程。">
      <template #actions>
        <el-button v-if="canManage" type="primary" :icon="Plus" @click="openCreateDialog">
          新增订单
        </el-button>
      </template>
    </PageHeader>

    <el-card class="search-card" shadow="never">
      <el-form :model="filters" inline @submit.prevent="loadOrders(1)">
        <el-form-item label="产品物料 ID">
          <el-input v-model.trim="filters.materialId" clearable placeholder="按物料 ID 查询" />
        </el-form-item>
        <el-form-item label="订单状态">
          <el-select v-model="filters.status" clearable placeholder="全部" style="width: 140px">
            <el-option label="待审核" value="pending_review" />
            <el-option label="待排产" value="pending_schedule" />
            <el-option label="生产中" value="in_progress" />
            <el-option label="已完工" value="completed" />
            <el-option label="已取消" value="cancelled" />
          </el-select>
        </el-form-item>
        <el-form-item label="计划完工起">
          <el-date-picker
            v-model="filters.planEndStart"
            placeholder="开始日期"
            type="date"
            value-format="YYYY-MM-DD"
          />
        </el-form-item>
        <el-form-item label="计划完工止">
          <el-date-picker
            v-model="filters.planEndEnd"
            placeholder="结束日期"
            type="date"
            value-format="YYYY-MM-DD"
          />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" :loading="loading" @click="loadOrders(1)">查询</el-button>
          <el-button :disabled="loading" :icon="Refresh" @click="resetFilters">重置</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card class="table-card" shadow="never">
      <el-alert
        v-if="error"
        class="request-error"
        :closable="false"
        show-icon
        :title="error"
        type="error"
      >
        <template #default>
          <el-button link type="primary" @click="loadOrders(page)">重新加载</el-button>
        </template>
      </el-alert>

      <el-table v-else v-loading="loading" :data="result.items" min-height="320" stripe>
        <el-table-column label="订单号" min-width="90" prop="orderId" />
        <el-table-column label="产品" min-width="150">
          <template #default="{ row }">{{
            row.materialName || `物料 #${row.materialId}`
          }}</template>
        </el-table-column>
        <el-table-column label="BOM 版本" min-width="120">
          <template #default="{ row }">{{ row.versionNo || `#${row.versionId}` }}</template>
        </el-table-column>
        <el-table-column label="计划数量" min-width="90" prop="planQty" />
        <el-table-column label="完工数量" min-width="90">
          <template #default="{ row }">{{ row.finishedQty ?? '-' }}</template>
        </el-table-column>
        <el-table-column label="生产进度" min-width="150">
          <template #default="{ row }">
            <el-progress :percentage="progressPercentage(row)" :stroke-width="10" />
          </template>
        </el-table-column>
        <el-table-column label="状态" min-width="90">
          <template #default="{ row }"
            ><StatusTag :labels="statusLabels" :value="row.status"
          /></template>
        </el-table-column>
        <el-table-column label="计划开工" min-width="120">
          <template #default="{ row }">{{ row.planStart || '-' }}</template>
        </el-table-column>
        <el-table-column label="计划完工" min-width="120">
          <template #default="{ row }">{{ row.planEnd || '-' }}</template>
        </el-table-column>
        <el-table-column label="排产产线" min-width="150">
          <template #default="{ row }">{{ row.schedule?.lineName || '未排产' }}</template>
        </el-table-column>
        <el-table-column fixed="right" label="操作" min-width="360">
          <template #default="{ row }">
            <el-button link type="primary" :icon="View" @click="openDetail(row)">详情</el-button>
            <template v-if="canManage">
              <el-button
                v-if="canReview(row)"
                link
                :disabled="actionSubmitting"
                type="primary"
                @click="approveOrder(row)"
                >审核</el-button
              >
              <el-button
                v-if="canEdit(row)"
                link
                type="primary"
                :icon="EditPen"
                @click="openEditDialog(row)"
                >修改</el-button
              >
              <el-button
                v-if="canStart(row)"
                link
                :disabled="actionSubmitting"
                type="success"
                @click="startOrder(row)"
                >开工</el-button
              >
              <el-button
                v-if="row.status === 'pending_schedule'"
                link
                :disabled="scheduleSubmitting"
                :icon="Calendar"
                type="primary"
                @click="openScheduleDialog(row)"
                >{{ row.schedule ? '修改排产' : '订单排产' }}</el-button
              >
              <el-button
                v-if="canFinish(row)"
                link
                :disabled="progressSubmitting"
                :icon="TrendCharts"
                type="primary"
                @click="openProgressDialog(row)"
                >上报进度</el-button
              >
              <el-button
                v-if="canFinish(row)"
                link
                :disabled="actionSubmitting"
                type="success"
                @click="finishOrder(row)"
                >完工</el-button
              >
              <el-button
                v-if="canCancel(row)"
                link
                :disabled="actionSubmitting"
                type="danger"
                @click="cancelOrder(row)"
                >取消</el-button
              >
            </template>
          </template>
        </el-table-column>
      </el-table>

      <el-empty
        v-if="!loading && !error && !result.items.length"
        description="暂无符合条件的生产订单"
      />

      <div v-if="!error && result.total > 0" class="pagination">
        <el-pagination
          v-model:current-page="page"
          background
          layout="total, prev, pager, next"
          :page-size="pageSize"
          :total="result.total"
          @current-change="loadOrders"
        />
      </div>
    </el-card>

    <el-dialog
      v-model="orderDialogVisible"
      :close-on-click-modal="false"
      :title="orderDialogTitle"
      width="560px"
    >
      <el-form ref="orderFormRef" :model="orderForm" :rules="orderRules" label-width="120px">
        <el-form-item label="产品物料 ID" prop="materialId">
          <el-input-number v-model="orderForm.materialId" :min="1" style="width: 100%" />
        </el-form-item>
        <el-form-item label="BOM 版本 ID" prop="versionId">
          <el-input-number v-model="orderForm.versionId" :min="1" style="width: 100%" />
        </el-form-item>
        <el-form-item label="计划数量" prop="planQty">
          <el-input-number v-model="orderForm.planQty" :min="1" style="width: 100%" />
        </el-form-item>
        <el-form-item label="计划开工时间" prop="planStart">
          <el-date-picker
            v-model="orderForm.planStart"
            style="width: 100%"
            type="datetime"
            value-format="YYYY-MM-DD HH:mm:ss"
          />
        </el-form-item>
        <el-form-item label="计划完工时间" prop="planEnd">
          <el-date-picker
            v-model="orderForm.planEnd"
            style="width: 100%"
            type="datetime"
            value-format="YYYY-MM-DD HH:mm:ss"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="orderDialogVisible = false">取消</el-button>
        <el-button :loading="submitting" type="primary" @click="submitOrderForm">保存</el-button>
      </template>
    </el-dialog>

    <el-drawer v-model="detailVisible" size="420px" title="生产订单详情">
      <el-alert v-if="detailError" :closable="false" show-icon :title="detailError" type="error" />
      <el-skeleton v-else-if="detailLoading" animated :rows="6" />
      <el-descriptions v-else-if="detail" border :column="1">
        <el-descriptions-item label="订单号">{{ detail.orderId }}</el-descriptions-item>
        <el-descriptions-item label="产品">{{
          detail.materialName || `物料 #${detail.materialId}`
        }}</el-descriptions-item>
        <el-descriptions-item label="BOM 版本">{{
          detail.versionNo || `#${detail.versionId}`
        }}</el-descriptions-item>
        <el-descriptions-item label="状态">
          <StatusTag :labels="statusLabels" :value="detail.status" />
        </el-descriptions-item>
        <el-descriptions-item label="计划数量">{{ detail.planQty }}</el-descriptions-item>
        <el-descriptions-item label="完工数量">{{
          detail.finishedQty ?? '-'
        }}</el-descriptions-item>
        <el-descriptions-item label="生产进度">
          <el-progress :percentage="progressPercentage(detail)" />
        </el-descriptions-item>
        <el-descriptions-item label="最近上报时间">{{
          formatDateTime(detail.lastProgressReportedAt)
        }}</el-descriptions-item>
        <el-descriptions-item label="最近上报备注">{{
          detail.lastProgressRemark || '-'
        }}</el-descriptions-item>
        <el-descriptions-item label="计划开工">{{ detail.planStart || '-' }}</el-descriptions-item>
        <el-descriptions-item label="计划完工">{{ detail.planEnd || '-' }}</el-descriptions-item>
        <el-descriptions-item label="分配产线">{{
          detail.schedule?.lineName || '未排产'
        }}</el-descriptions-item>
        <el-descriptions-item label="排产时段">{{
          detail.schedule
            ? `${formatDateTime(detail.schedule.plannedStart)} 至 ${formatDateTime(detail.schedule.plannedEnd)}`
            : '-'
        }}</el-descriptions-item>
        <el-descriptions-item label="实际开工">{{
          formatDateTime(detail.actualStart)
        }}</el-descriptions-item>
        <el-descriptions-item label="实际完工">{{
          formatDateTime(detail.actualEnd)
        }}</el-descriptions-item>
        <el-descriptions-item label="审核意见">{{
          detail.reviewComment || '-'
        }}</el-descriptions-item>
      </el-descriptions>
      <template v-if="detail && detailStages.length">
        <el-divider content-position="left">生产阶段</el-divider>
        <el-steps direction="vertical" :active="detailStages.length">
          <el-step
            v-for="stage in detailStages"
            :key="stage.name"
            :description="formatDateTime(stage.completedAt || stage.startedAt)"
            :status="getStageStatus(stage)"
            :title="stage.name"
          />
        </el-steps>
      </template>
      <el-empty v-if="!detail && !detailLoading && !detailError" description="暂无详情数据" />
    </el-drawer>

    <el-dialog
      v-model="progressDialogVisible"
      :title="`上报订单进度 #${progressTarget?.orderId || ''}`"
      width="520px"
    >
      <el-alert
        :closable="false"
        :title="`计划 ${progressTarget?.planQty || 0}，当前已完成 ${progressTarget?.finishedQty || 0}`"
        type="info"
      />
      <el-form
        ref="progressFormRef"
        :model="progressForm"
        :rules="progressRules"
        label-width="100px"
        class="dialog-form"
      >
        <el-form-item label="本次完成" prop="completedQty">
          <el-input-number v-model="progressForm.completedQty" :min="1" style="width: 100%" />
        </el-form-item>
        <el-form-item label="上报时间" prop="reportedAt">
          <el-date-picker
            v-model="progressForm.reportedAt"
            type="datetime"
            value-format="YYYY-MM-DD HH:mm:ss"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model.trim="progressForm.remark" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button :disabled="progressSubmitting" @click="progressDialogVisible = false"
          >取消</el-button
        >
        <el-button :loading="progressSubmitting" type="primary" @click="submitProgress"
          >提交</el-button
        >
      </template>
    </el-dialog>

    <el-dialog
      v-model="scheduleDialogVisible"
      :title="`订单排产 #${scheduleTarget?.orderId || ''}`"
      width="560px"
    >
      <el-form
        ref="scheduleFormRef"
        :model="scheduleForm"
        :rules="scheduleRules"
        label-width="110px"
      >
        <el-form-item label="可用生产线" prop="lineId">
          <el-select
            v-model="scheduleForm.lineId"
            filterable
            :loading="lineLoading"
            style="width: 100%"
            placeholder="选择生产线"
          >
            <el-option
              v-for="line in lineOptions"
              :key="line.lineId"
              :label="`生产线 ${line.lineId} · ${line.typeName || '-'} · ${line.status === 'idle' ? '空闲' : '运行中'}`"
              :value="line.lineId"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="计划开始" prop="plannedStart">
          <el-date-picker
            v-model="scheduleForm.plannedStart"
            type="datetime"
            value-format="YYYY-MM-DDTHH:mm:ss"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="计划结束" prop="plannedEnd">
          <el-date-picker
            v-model="scheduleForm.plannedEnd"
            type="datetime"
            value-format="YYYY-MM-DDTHH:mm:ss"
            style="width: 100%"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button :disabled="scheduleSubmitting" @click="scheduleDialogVisible = false"
          >取消</el-button
        >
        <el-button :loading="scheduleSubmitting" type="primary" @click="submitSchedule"
          >保存排产</el-button
        >
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.search-card {
  margin-bottom: 16px;
}
.request-error {
  margin-bottom: 16px;
}
.pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
.dialog-form {
  margin-top: 16px;
}
</style>
