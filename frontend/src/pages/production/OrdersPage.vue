<script setup lang="ts">
import { EditPen, Plus, Refresh, View } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import {
  type ProductionCompletionReportFormData,
  type ProductionOrderFormData,
  type ProductionOrderItem,
  type ProductionOrderMaterialLockPreviewItem,
  type ProductionOrderProductOption,
  type ProductionOrderStatus,
  productionService,
} from '@/services/ProductionService'
import { computed, onMounted, reactive, ref } from 'vue'
import { PermissionCode } from '@/constants/permissions'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import type { PageResult } from '@/services/pagination'
import StatusTag from '@/components/common/StatusTag.vue'
import { formatDateTime, formatNumber } from '@/utils/format'
import { getErrorMessage } from '@/utils/error'
import { productionOrderStatusLabels as statusLabels } from '@/constants/status'
import { useAuthStore } from '@/stores/auth'
import { materialService } from '@/services/MaterialService'
import type { MaterialNameOption } from '@/types/material'

const pageSize = 10
const auth = useAuthStore()
const filters = reactive({
  materialId: undefined as number | undefined,
  planEndEnd: '',
  planEndStart: '',
  status: '',
})
const page = ref(1)
const loading = ref(false)
const error = ref('')
const result = ref<PageResult<ProductionOrderItem>>({ items: [], page: 1, pageSize, total: 0 })

const canCreateOrder = computed(() => auth.hasPermission(PermissionCode.ProductionOrderCreate))
const canUpdateOrder = computed(() => auth.hasPermission(PermissionCode.ProductionOrderUpdate))
const canApproveOrder = computed(() => auth.hasPermission(PermissionCode.ProductionOrderApprove))
const canStartOrder = computed(() => auth.hasPermission(PermissionCode.ProductionOrderStart))
const canFinishOrder = computed(() => auth.hasPermission(PermissionCode.ProductionOrderFinish))
const canCancelOrder = computed(() => auth.hasPermission(PermissionCode.ProductionOrderCancel))

const orderDialogVisible = ref(false)
const orderDialogMode = ref<'create' | 'edit'>('create')
const orderFormRef = ref<FormInstance>()
const editingOrderId = ref<number>()
const editingOrderStatus = ref<ProductionOrderStatus>()
const submitting = ref(false)
const actionSubmitting = ref(false)
const reviewDialogVisible = ref(false)
const reviewLoading = ref(false)
const reviewError = ref('')
const reviewComment = ref('')
const reviewingOrder = ref<ProductionOrderItem>()
const reviewPreview = ref<ProductionOrderMaterialLockPreviewItem>()
let reviewRequestId = 0
const detailVisible = ref(false)
const detailLoading = ref(false)
const detailError = ref('')
const detail = ref<ProductionOrderItem>()
const reportDialogVisible = ref(false)
const reportFormRef = ref<FormInstance>()
const reportingOrder = ref<ProductionOrderItem>()
const reportSubmitting = ref(false)
const productOptions = ref<ProductionOrderProductOption[]>([])
const productOptionsLoading = ref(false)
const productOptionsError = ref('')
let productOptionsLoaded = false
let productOptionsPromise: Promise<void> | undefined = undefined
const productMaterialOptions = ref<MaterialNameOption[]>([])
const productMaterialOptionsLoading = ref(false)
const productMaterialOptionsError = ref('')

interface ProductionOrderFormModel extends Omit<ProductionOrderFormData, 'versionId'> {
  versionId?: number
}

const orderForm = reactive<ProductionOrderFormModel>({
  materialId: 0,
  planEnd: '',
  planQty: 1,
  planStart: '',
  versionId: undefined,
})
const orderDialogTitle = computed(() => {
  if (orderDialogMode.value === 'create') {
    return '新增生产订单'
  }
  return '修改生产订单计划'
})
const orderRequirementsLocked = computed(() => editingOrderStatus.value === 'pending_schedule')

const orderRules: FormRules<ProductionOrderFormModel> = {
  planEnd: [{ message: '请选择计划完工日期', required: true, trigger: 'change' }],
  planQty: [
    { message: '请输入计划数量', required: true, trigger: 'blur', type: 'number' },
    { message: '计划数量必须大于 0', min: 1, trigger: 'blur', type: 'number' },
  ],
  planStart: [{ message: '请选择计划开工日期', required: true, trigger: 'change' }],
  versionId: [
    { message: '请选择当前生效的产品 BOM', required: true, trigger: 'change', type: 'number' },
  ],
}

const reportForm = reactive<ProductionCompletionReportFormData>({
  batchNo: '',
  finishQty: 1,
  orderId: 0,
  qualifiedQty: 1,
})

const reportRules: FormRules<ProductionCompletionReportFormData> = {
  batchNo: [
    { message: '请输入生产批次号', required: true, trigger: 'blur' },
    { max: 30, message: '批次号不能超过 30 个字符', trigger: 'blur' },
  ],
  finishQty: [
    { message: '请输入本批完工数量', required: true, trigger: 'blur', type: 'number' },
    { message: '本批完工数量必须大于 0', min: 0.01, trigger: 'blur', type: 'number' },
  ],
  qualifiedQty: [
    {
      trigger: 'change',
      validator: (_rule, value, callback) => {
        if (typeof value !== 'number' || value < 0) {
          callback(new Error('合格数量不能小于 0'))
        } else if (value > reportForm.finishQty) {
          callback(new Error('合格数量不能大于本批完工数量'))
        } else {
          callback()
        }
      },
    },
  ],
}

const reportingRemainingQty = computed(() => {
  const order = reportingOrder.value
  if (!order) {
    return 0
  }
  return Math.max(0, order.planQty - (order.finishedQty ?? 0))
})

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

async function loadOrders(targetPage = page.value) {
  loading.value = true
  error.value = ''
  try {
    result.value = await productionService.listOrders({
      materialId: filters.materialId,
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
  Object.assign(filters, {
    materialId: undefined,
    planEndEnd: '',
    planEndStart: '',
    status: '',
  })
  void loadOrders(1)
}

async function loadProductMaterialOptions() {
  productMaterialOptionsLoading.value = true
  productMaterialOptionsError.value = ''
  try {
    productMaterialOptions.value = await materialService.listMaterialNameOptions([
      'semiFinished',
      'finished',
    ])
  } catch (requestError) {
    productMaterialOptions.value = []
    productMaterialOptionsError.value = getErrorMessage(requestError, '产品选项加载失败')
  } finally {
    productMaterialOptionsLoading.value = false
  }
}

function resetOrderForm() {
  Object.assign(orderForm, {
    materialId: 0,
    planEnd: '',
    planQty: 1,
    planStart: '',
    versionId: undefined,
  })
  editingOrderId.value = undefined
  editingOrderStatus.value = undefined
  orderFormRef.value?.clearValidate()
}

async function loadProductOptions(force = false) {
  if (productOptionsPromise) {
    await productOptionsPromise
    return
  }
  if (productOptionsLoaded && !force) {
    return
  }
  productOptionsPromise = (async () => {
    productOptionsLoading.value = true
    productOptionsError.value = ''
    try {
      productOptions.value = await productionService.listOrderProductOptions()
      productOptionsLoaded = true
    } catch (requestError) {
      productOptions.value = []
      productOptionsLoaded = false
      productOptionsError.value = getErrorMessage(requestError, '产品与 BOM 版本加载失败')
    } finally {
      productOptionsLoading.value = false
    }
  })()
  try {
    await productOptionsPromise
  } finally {
    productOptionsPromise = undefined
  }
}

function handleProductChange(versionId?: number) {
  const option = productOptions.value.find((item) => item.versionId === versionId)
  orderForm.materialId = option?.materialId ?? 0
}

function openCreateDialog() {
  orderDialogMode.value = 'create'
  resetOrderForm()
  orderDialogVisible.value = true
  void loadProductOptions()
}

async function openEditDialog(order: ProductionOrderItem) {
  orderDialogMode.value = 'edit'
  editingOrderStatus.value = order.status
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
  if (order.status === 'pending_schedule') {
    return
  }
  await loadProductOptions()
  if (!orderDialogVisible.value || editingOrderId.value !== order.orderId) {
    return
  }
  const currentOption = productOptions.value.find(
    (item) => item.materialId === order.materialId && item.versionId === order.versionId,
  )
  const replacementOption =
    currentOption ?? productOptions.value.find((item) => item.materialId === order.materialId)
  orderForm.versionId = replacementOption?.versionId
  orderForm.materialId = replacementOption?.materialId ?? 0
}

async function submitOrderForm() {
  const valid = await orderFormRef.value?.validate().catch(() => false)
  if (!valid || submitting.value) {
    return
  }
  const selectedProduct = productOptions.value.find(
    (item) => item.versionId === orderForm.versionId,
  )
  if (!orderRequirementsLocked.value && !selectedProduct) {
    ElMessage.warning('请选择当前生效的产品 BOM')
    return
  }
  if (orderForm.planEnd < orderForm.planStart) {
    ElMessage.warning('计划完工日期不得早于计划开工日期')
    return
  }
  submitting.value = true
  try {
    const selection = (() => {
      if (orderRequirementsLocked.value) {
        return { materialId: orderForm.materialId, versionId: orderForm.versionId! }
      }
      return selectedProduct!
    })()
    const { materialId, versionId } = selection
    const request: ProductionOrderFormData = {
      ...orderForm,
      materialId,
      versionId,
    }
    if (orderDialogMode.value === 'create') {
      await productionService.createOrder(request)
      ElMessage.success('生产订单已创建')
    } else if (editingOrderId.value !== undefined) {
      await productionService.updateOrder(editingOrderId.value, request)
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
  try {
    detail.value = await productionService.getOrder(order.orderId)
  } catch (requestError) {
    detailError.value = getErrorMessage(requestError, '生产订单详情加载失败')
  } finally {
    detailLoading.value = false
  }
}

async function loadReviewPreview() {
  const order = reviewingOrder.value
  if (!order) {
    return
  }
  const currentRequestId = ++reviewRequestId
  reviewLoading.value = true
  reviewError.value = ''
  reviewPreview.value = undefined
  try {
    const preview = await productionService.previewOrderMaterialLock(order.orderId)
    if (currentRequestId === reviewRequestId) {
      reviewPreview.value = preview
    }
  } catch (requestError) {
    if (currentRequestId === reviewRequestId) {
      reviewError.value = getErrorMessage(requestError, '物料锁定预览失败')
    }
  } finally {
    if (currentRequestId === reviewRequestId) {
      reviewLoading.value = false
    }
  }
}

function openReviewDialog(order: ProductionOrderItem) {
  reviewingOrder.value = order
  reviewComment.value = ''
  reviewDialogVisible.value = true
  void loadReviewPreview()
}

async function submitReview(approved: boolean) {
  const order = reviewingOrder.value
  if (!order || actionSubmitting.value) {
    return
  }
  if (approved && !reviewPreview.value?.canApprove) {
    ElMessage.warning('物料库存不足，无法通过审核')
    return
  }

  try {
    if (!approved) {
      await ElMessageBox.confirm('确定拒绝该生产订单吗？', '拒绝生产订单', {
        confirmButtonText: '确认拒绝',
        type: 'warning',
      })
    }
    actionSubmitting.value = true
    await productionService.approveOrder(order.orderId, approved, reviewComment.value)
    let successMessage = '生产订单已拒绝'
    if (approved) {
      successMessage = '订单已通过审核并锁定生产物料'
    }
    ElMessage.success(successMessage)
    reviewDialogVisible.value = false
    await loadOrders(page.value)
  } catch (requestError) {
    if (requestError !== 'cancel' && requestError !== 'close') {
      let failureMessage = '拒绝生产订单失败'
      if (approved) {
        failureMessage = '审核生产订单失败'
      }
      ElMessage.error(getErrorMessage(requestError, failureMessage))
      if (approved) {
        await loadReviewPreview()
      }
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

function openCompletionReport(order: ProductionOrderItem) {
  const defaultQty = Math.max(0.01, order.planQty - (order.finishedQty ?? 0))
  reportingOrder.value = order
  Object.assign(reportForm, {
    batchNo: '',
    finishQty: defaultQty,
    orderId: order.orderId,
    qualifiedQty: defaultQty,
  })
  reportFormRef.value?.clearValidate()
  reportDialogVisible.value = true
}

async function submitCompletionReport() {
  const valid = await reportFormRef.value?.validate().catch(() => false)
  if (!valid || reportSubmitting.value) {
    return
  }
  reportSubmitting.value = true
  try {
    const report = await productionService.reportProductionCompletion({
      ...reportForm,
      batchNo: reportForm.batchNo.trim(),
    })
    if (report.orderCompleted) {
      ElMessage.success('本批报工成功，生产订单已自动完工')
    } else {
      ElMessage.success(`本批报工成功，累计合格数量 ${report.productionOrder.finishedQty ?? 0}`)
    }
    reportDialogVisible.value = false
    await loadOrders(page.value)
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '生产订单报工失败'))
  } finally {
    reportSubmitting.value = false
  }
}

function isReviewable(order: ProductionOrderItem) {
  return order.status === 'pending_review'
}
function isEditable(order: ProductionOrderItem) {
  return order.status === 'pending_review' || order.status === 'pending_schedule'
}
function isStartable(order: ProductionOrderItem) {
  return order.status === 'pending_schedule'
}
function isFinishable(order: ProductionOrderItem) {
  return order.status === 'in_progress'
}
function isCancellable(order: ProductionOrderItem) {
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
  void loadProductMaterialOptions()
  if (canCreateOrder.value || canUpdateOrder.value) {
    void loadProductOptions()
  }
})
</script>

<template>
  <PageContainer>
    <PageHeader title="生产订单" description="按状态管理生产订单的审核、开工、完工与取消流程。">
      <template #actions>
        <el-button v-if="canCreateOrder" type="primary" :icon="Plus" @click="openCreateDialog">
          新增订单
        </el-button>
      </template>
    </PageHeader>

    <el-card class="search-card" shadow="never">
      <el-alert
        v-if="productMaterialOptionsError"
        class="request-error"
        :closable="false"
        show-icon
        :title="productMaterialOptionsError"
        type="warning"
      >
        <template #default>
          <el-button link type="primary" @click="loadProductMaterialOptions">
            重新加载产品选项
          </el-button>
        </template>
      </el-alert>
      <el-form :model="filters" inline @submit.prevent="loadOrders(1)">
        <el-form-item label="产品名">
          <el-select
            v-model="filters.materialId"
            clearable
            filterable
            :loading="productMaterialOptionsLoading"
            no-data-text="暂无半成品或成品"
            placeholder="选择产品"
            style="width: 220px"
          >
            <el-option
              v-for="material in productMaterialOptions"
              :key="material.materialId"
              :label="material.materialName"
              :value="material.materialId"
            />
          </el-select>
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
        <el-table-column label="订单 ID" min-width="70" prop="orderId" />
        <el-table-column label="产品" min-width="150">
          <template #default="{ row }">{{
            row.materialName || `物料 #${row.materialId}`
          }}</template>
        </el-table-column>
        <el-table-column label="BOM 版本" min-width="90">
          <template #default="{ row }">{{ row.versionNo || `#${row.versionId}` }}</template>
        </el-table-column>
        <el-table-column label="计划数量" min-width="80" prop="planQty" />
        <el-table-column label="完工数量" min-width="80">
          <template #default="{ row }">{{ row.finishedQty ?? '-' }}</template>
        </el-table-column>
        <el-table-column label="完工比例" min-width="150">
          <template #default="{ row }">
            <el-progress
              v-if="row.finishedQty !== undefined"
              :percentage="progressPercentage(row)"
              :stroke-width="10"
            />
            <span v-else>-</span>
          </template>
        </el-table-column>
        <el-table-column label="状态" min-width="80">
          <template #default="{ row }"
            ><StatusTag :labels="statusLabels" :value="row.status"
          /></template>
        </el-table-column>
        <el-table-column label="计划开工" min-width="100">
          <template #default="{ row }">{{ row.planStart || '-' }}</template>
        </el-table-column>
        <el-table-column label="计划完工" min-width="100">
          <template #default="{ row }">{{ row.planEnd || '-' }}</template>
        </el-table-column>
        <el-table-column fixed="right" label="操作" min-width="260">
          <template #default="{ row }">
            <el-button link type="primary" :icon="View" @click="openDetail(row)">详情</el-button>
            <el-button
              v-if="canApproveOrder && isReviewable(row)"
              link
              :disabled="actionSubmitting"
              type="primary"
              @click="openReviewDialog(row)"
              >审核</el-button
            >
            <el-button
              v-if="canUpdateOrder && isEditable(row)"
              link
              type="primary"
              :icon="EditPen"
              @click="openEditDialog(row)"
              >计划排期</el-button
            >
            <el-button
              v-if="canStartOrder && isStartable(row)"
              link
              :disabled="actionSubmitting"
              type="success"
              @click="startOrder(row)"
              >开工</el-button
            >
            <el-button
              v-if="canFinishOrder && isFinishable(row)"
              link
              :disabled="reportSubmitting"
              type="primary"
              @click="openCompletionReport(row)"
              >产线报工</el-button
            >
            <el-button
              v-if="canCancelOrder && isCancellable(row)"
              link
              :disabled="actionSubmitting"
              type="danger"
              @click="cancelOrder(row)"
              >取消</el-button
            >
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
      <el-alert
        v-if="productOptionsError"
        class="request-error"
        :closable="false"
        show-icon
        :title="productOptionsError"
        type="error"
      >
        <template #default>
          <el-button link type="primary" @click="loadProductOptions(true)">重新加载选项</el-button>
        </template>
      </el-alert>
      <el-alert
        v-if="orderRequirementsLocked"
        class="request-error"
        :closable="false"
        show-icon
        title="该订单已锁定生产物料，仅允许调整计划日期。"
        type="info"
      />
      <el-form ref="orderFormRef" :model="orderForm" :rules="orderRules" label-width="120px">
        <el-form-item label="产品" prop="versionId">
          <el-select
            v-model="orderForm.versionId"
            clearable
            :disabled="orderRequirementsLocked"
            filterable
            :loading="productOptionsLoading"
            no-data-text="暂无当前生效的产品 BOM"
            placeholder="请选择当前生效的产品 BOM"
            style="width: 100%"
            @change="handleProductChange"
          >
            <el-option
              v-for="option in productOptions"
              :key="option.versionId"
              :label="`${option.materialName} ${option.versionNo} #${option.materialId}`"
              :value="option.versionId"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="计划数量" prop="planQty">
          <el-input-number
            :controls="false"
            v-model="orderForm.planQty"
            :disabled="orderRequirementsLocked"
            :min="1"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="计划开工日期" prop="planStart">
          <el-date-picker
            v-model="orderForm.planStart"
            style="width: 100%"
            type="date"
            value-format="YYYY-MM-DD"
          />
        </el-form-item>
        <el-form-item label="计划完工日期" prop="planEnd">
          <el-date-picker
            v-model="orderForm.planEnd"
            style="width: 100%"
            type="date"
            value-format="YYYY-MM-DD"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="orderDialogVisible = false">取消</el-button>
        <el-button
          :disabled="!orderRequirementsLocked && (productOptionsLoading || !productOptions.length)"
          :loading="submitting"
          type="primary"
          @click="submitOrderForm"
          >保存</el-button
        >
      </template>
    </el-dialog>

    <el-dialog
      v-model="reviewDialogVisible"
      :close-on-click-modal="false"
      title="审核生产订单"
      width="880px"
    >
      <el-alert
        v-if="reviewingOrder"
        class="request-error"
        :closable="false"
        :title="`订单 #${reviewingOrder.orderId} · ${
          reviewingOrder.materialName || `物料 #${reviewingOrder.materialId}`
        } · 计划数量 ${reviewingOrder.planQty}`"
        type="info"
      />
      <el-skeleton v-if="reviewLoading" animated :rows="6" />
      <el-alert
        v-else-if="reviewError"
        class="request-error"
        :closable="false"
        show-icon
        :title="reviewError"
        type="error"
      >
        <template #default>
          <el-button link type="primary" @click="loadReviewPreview">重新计算</el-button>
        </template>
      </el-alert>
      <template v-else-if="reviewPreview">
        <el-alert
          class="request-error"
          :closable="false"
          show-icon
          :title="
            reviewPreview.canApprove
              ? '当前库存充足，审核通过后将立即锁定以下物料。'
              : '当前库存不足，不能通过审核。'
          "
          :type="reviewPreview.canApprove ? 'success' : 'warning'"
        />
        <el-table :data="reviewPreview.items" max-height="360" stripe>
          <el-table-column label="物料" min-width="150">
            <template #default="{ row }">
              {{ row.materialName }}（#{{ row.materialId }}）
            </template>
          </el-table-column>
          <el-table-column label="BOM 用量" min-width="95">
            <template #default="{ row }">{{ formatNumber(row.bomQuantity) }}</template>
          </el-table-column>
          <el-table-column label="损耗率" min-width="80">
            <template #default="{ row }">{{ formatNumber(row.lossRate * 100) }}%</template>
          </el-table-column>
          <el-table-column label="订单需求" min-width="100">
            <template #default="{ row }">
              {{ formatNumber(row.requiredQty) }} {{ row.unit }}
            </template>
          </el-table-column>
          <el-table-column label="已锁定" min-width="90">
            <template #default="{ row }">{{ formatNumber(row.lockedQty) }}</template>
          </el-table-column>
          <el-table-column label="本次待锁" min-width="90">
            <template #default="{ row }">{{ formatNumber(row.pendingLockQty) }}</template>
          </el-table-column>
          <el-table-column label="可用库存" min-width="90">
            <template #default="{ row }">{{ formatNumber(row.availableQty) }}</template>
          </el-table-column>
          <el-table-column label="缺口" min-width="90">
            <template #default="{ row }">
              <span :class="{ 'shortage-value': row.shortageQty > 0 }">
                {{ formatNumber(row.shortageQty) }}
              </span>
            </template>
          </el-table-column>
        </el-table>
      </template>
      <el-form class="review-comment" label-width="80px">
        <el-form-item label="审核意见">
          <el-input
            v-model.trim="reviewComment"
            :disabled="actionSubmitting"
            maxlength="200"
            placeholder="可选"
            :rows="3"
            show-word-limit
            type="textarea"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button :disabled="actionSubmitting" type="danger" plain @click="submitReview(false)">
          拒绝订单
        </el-button>
        <el-button :disabled="actionSubmitting" @click="reviewDialogVisible = false">
          取消
        </el-button>
        <el-button
          :disabled="reviewLoading || !reviewPreview?.canApprove"
          :loading="actionSubmitting"
          type="primary"
          @click="submitReview(true)"
        >
          通过并锁定物料
        </el-button>
      </template>
    </el-dialog>

    <el-dialog
      v-model="reportDialogVisible"
      :close-on-click-modal="false"
      title="生产订单完工报工"
      width="520px"
      @closed="reportFormRef?.resetFields()"
    >
      <el-alert
        v-if="reportingOrder"
        :closable="false"
        :title="`订单 #${reportingOrder.orderId} · ${
          reportingOrder.materialName || `物料 #${reportingOrder.materialId}`
        } · 尚需合格 ${reportingRemainingQty}`"
        type="info"
      />
      <el-form
        ref="reportFormRef"
        class="report-form"
        :model="reportForm"
        :rules="reportRules"
        label-width="120px"
      >
        <el-form-item label="本批完工数量" prop="finishQty">
          <el-input-number
            v-model="reportForm.finishQty"
            :controls="false"
            :min="0.01"
            :precision="2"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="合格数量" prop="qualifiedQty">
          <el-input-number
            v-model="reportForm.qualifiedQty"
            :controls="false"
            :max="reportForm.finishQty"
            :min="0"
            :precision="2"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="批次号" prop="batchNo">
          <el-input
            v-model.trim="reportForm.batchNo"
            maxlength="30"
            placeholder="请输入本次报工的唯一批次号"
            show-word-limit
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button :disabled="reportSubmitting" @click="reportDialogVisible = false"
          >取消</el-button
        >
        <el-button :loading="reportSubmitting" type="primary" @click="submitCompletionReport">
          确认报工
        </el-button>
      </template>
    </el-dialog>

    <el-drawer v-model="detailVisible" size="420px" title="生产订单详情">
      <el-alert v-if="detailError" :closable="false" show-icon :title="detailError" type="error" />
      <el-skeleton v-else-if="detailLoading" animated :rows="6" />
      <el-descriptions v-else-if="detail" border :column="1">
        <el-descriptions-item label="订单 ID">{{ detail.orderId }}</el-descriptions-item>
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
        <el-descriptions-item label="完工比例">
          <el-progress
            v-if="detail.finishedQty !== undefined"
            :percentage="progressPercentage(detail)"
          />
          <span v-else>-</span>
        </el-descriptions-item>
        <el-descriptions-item label="计划开工">{{ detail.planStart || '-' }}</el-descriptions-item>
        <el-descriptions-item label="计划完工">{{ detail.planEnd || '-' }}</el-descriptions-item>
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
      <el-empty v-if="!detail && !detailLoading && !detailError" description="暂无详情数据" />
    </el-drawer>
  </PageContainer>
</template>

<style scoped>
.search-card {
  margin-bottom: 16px;
}
.request-error {
  margin-bottom: 16px;
}
.report-form {
  margin-top: 18px;
}
.review-comment {
  margin-top: 18px;
}
.shortage-value {
  color: var(--el-color-danger);
  font-weight: 600;
}
.pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
</style>
