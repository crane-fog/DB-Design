<script setup lang="ts">
import {
  type CapacityBalanceItem,
  type CapacityDetectionItem,
  type ExternalOrderConvertItem,
  type ExternalOrderItem,
  type ProductionCapacityEstimateFormData,
  type ProductionCapacityEstimateItem,
  type ProductionLineItem,
  type ProductionLineRunStatus,
  type ProductionLineStatusItem,
  productionService,
} from '@/services/ProductionService'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Refresh, Search } from '@element-plus/icons-vue'
import { computed, onMounted, reactive, ref } from 'vue'
import { formatDateTime, formatNumber } from '@/utils/format'
import type { MaterialShortageItem } from '@/types/inventory'
import { PERMISSIONS } from '@/constants/permissions'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import type { PageResult } from '@/services/pagination'
import StatusTag from '@/components/common/StatusTag.vue'
import { getErrorMessage } from '@/utils/error'
import { inventoryService } from '@/services/InventoryService'
import { parsePositiveInt } from '@/utils/parse'
import { useAuthStore } from '@/stores/auth'
import { useRoute } from 'vue-router'

type OperationsTab = 'balance' | 'detection' | 'estimate' | 'external' | 'status'

const externalStatusLabels = {
  accepted: '已接受',
  pending_review: '待审核',
  rejected: '已拒绝',
}
const lineStatusLabels = { fault: '故障', idle: '空闲', running: '运行中' }

const auth = useAuthStore()
const route = useRoute()
const isExternalCustomer = computed(() => auth.hasRole('外部客户'))
const canManageOrders = computed(() => auth.hasPermission(PERMISSIONS.production.orders))
const canManageCapacity = computed(() => auth.hasPermission(PERMISSIONS.production.capacity))
const hasOperationsAccess = computed(
  () => isExternalCustomer.value || canManageOrders.value || canManageCapacity.value,
)
let initialTab: OperationsTab = 'estimate'
if (isExternalCustomer.value || canManageOrders.value) {
  initialTab = 'external'
}
if (route.query.tab === 'status' && canManageCapacity.value) {
  initialTab = 'status'
}
const activeTab = ref<OperationsTab>(initialTab)

// ---------- 外部订单 ----------
const externalPageSize = 10
const externalPage = ref(1)
const externalLoading = ref(false)
const externalError = ref('')
const externalFilters = reactive({ customerId: '', status: '' })
const externalResult = ref<PageResult<ExternalOrderItem>>({
  items: [],
  page: 1,
  pageSize: externalPageSize,
  total: 0,
})
const externalCreateVisible = ref(false)
const externalSubmitting = ref(false)
const externalForm = reactive({
  contactPerson: '',
  contactPhone: '',
  customerId: 0,
  expectedDate: '',
  materialId: 0,
  quantity: 1,
})

const convertVisible = ref(false)
const convertingOrder = ref<ExternalOrderItem>()
const convertResult = ref<ExternalOrderConvertItem>()
const convertSubmitting = ref(false)
const convertForm = reactive({
  materialId: 0,
  planEnd: '',
  planQty: 1,
  planStart: '',
  versionId: 0,
})

function selectedExternalStatus() {
  const { status } = externalFilters
  if (status === 'accepted' || status === 'pending_review' || status === 'rejected') {
    return status
  }
  return undefined
}

async function loadExternalOrders(targetPage = externalPage.value) {
  externalLoading.value = true
  externalError.value = ''
  try {
    let customerId = undefined as number | undefined
    if (canManageOrders.value) {
      customerId = parsePositiveInt(externalFilters.customerId)
    }
    externalResult.value = await productionService.listExternalOrders({
      customerId,
      page: targetPage,
      pageSize: externalPageSize,
      status: selectedExternalStatus(),
    })
    externalPage.value = externalResult.value.page
  } catch (error) {
    externalError.value = getErrorMessage(error, '外部订单加载失败')
  } finally {
    externalLoading.value = false
  }
}

function resetExternalFilters() {
  Object.assign(externalFilters, { customerId: '', status: '' })
  void loadExternalOrders(1)
}

function openExternalCreate() {
  Object.assign(externalForm, {
    contactPerson: '',
    contactPhone: '',
    customerId: 0,
    expectedDate: '',
    materialId: 0,
    quantity: 1,
  })
  externalCreateVisible.value = true
}

async function submitExternalOrder() {
  if (
    externalForm.materialId <= 0 ||
    externalForm.quantity <= 0 ||
    !externalForm.expectedDate ||
    !externalForm.contactPerson.trim() ||
    !externalForm.contactPhone.trim()
  ) {
    ElMessage.warning('请完整填写产品、数量、日期和联系方式')
    return
  }
  if (canManageOrders.value && !isExternalCustomer.value && externalForm.customerId <= 0) {
    ElMessage.warning('管理员代录外部订单时必须填写客户 ID')
    return
  }
  externalSubmitting.value = true
  try {
    let customerId = undefined as number | undefined
    if (canManageOrders.value && !isExternalCustomer.value) {
      ;({ customerId } = externalForm)
    }
    await productionService.addExternalOrder({
      contactPerson: externalForm.contactPerson,
      contactPhone: externalForm.contactPhone,
      customerId,
      expectedDate: externalForm.expectedDate,
      materialId: externalForm.materialId,
      quantity: externalForm.quantity,
    })
    externalCreateVisible.value = false
    ElMessage.success('外部订单已提交')
    await loadExternalOrders(1)
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '外部订单提交失败'))
  } finally {
    externalSubmitting.value = false
  }
}

async function reviewExternalOrder(order: ExternalOrderItem, accepted: boolean) {
  try {
    let action = '拒绝'
    if (accepted) {
      action = '接受'
    }
    const { value } = await ElMessageBox.prompt(
      `请输入${action}意见（可选）`,
      `${action}外部订单`,
      {
        confirmButtonText: `确认${action}`,
        inputType: 'textarea',
      },
    )
    await productionService.reviewExternalOrder(order.extOrderId, accepted, value)
    ElMessage.success(`外部订单已${action}`)
    await loadExternalOrders(externalPage.value)
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') {
      ElMessage.error(getErrorMessage(error, '外部订单审核失败'))
    }
  }
}

function openConvert(order: ExternalOrderItem) {
  convertingOrder.value = order
  convertResult.value = undefined
  Object.assign(convertForm, {
    materialId: order.materialId,
    planEnd: order.expectedDate,
    planQty: order.quantity,
    planStart: '',
    versionId: 0,
  })
  convertVisible.value = true
}

async function submitConvert() {
  if (
    !convertingOrder.value ||
    convertForm.materialId <= 0 ||
    convertForm.versionId <= 0 ||
    convertForm.planQty <= 0 ||
    !convertForm.planStart ||
    !convertForm.planEnd
  ) {
    ElMessage.warning('请完整填写生产订单计划')
    return
  }
  if (convertForm.planEnd < convertForm.planStart) {
    ElMessage.warning('计划完工日期不得早于计划开工日期')
    return
  }
  convertSubmitting.value = true
  try {
    const result = await productionService.convertExternalOrder({
      extOrderId: convertingOrder.value.extOrderId,
      productionOrders: [{ ...convertForm }],
    })
    convertResult.value = result
    convertVisible.value = false
    const orderIds = result.productionOrders.map((order) => `#${order.orderId}`).join('、')
    ElMessage.success(`已转换为生产订单 ${orderIds}`)
    await loadExternalOrders(externalPage.value)
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '外部订单转换失败'))
  } finally {
    convertSubmitting.value = false
  }
}

// ---------- 交付能力评估 ----------
const estimateMode = ref<'order' | 'temporary'>('order')
const estimateLoading = ref(false)
const estimateError = ref('')
const estimateResult = ref<ProductionCapacityEstimateItem>()
const estimateShortages = ref<MaterialShortageItem[]>([])
const estimateForm = reactive({
  expectedDate: '',
  materialId: 0,
  orderId: 0,
  planQty: 1,
  versionId: 0,
})

async function resolveEstimateRequest() {
  if (estimateMode.value === 'order') {
    const order = await productionService.getOrder(estimateForm.orderId)
    if (!order) {
      throw new Error('未找到生产订单')
    }
    return {
      request: { orderId: order.orderId },
      shortage: {
        materialId: order.materialId,
        productionQty: order.planQty,
        versionId: order.versionId,
      },
    }
  }
  return {
    request: {
      expectedDate: estimateForm.expectedDate,
      materialId: estimateForm.materialId,
      planQty: estimateForm.planQty,
      versionId: estimateForm.versionId,
    },
    shortage: {
      materialId: estimateForm.materialId,
      productionQty: estimateForm.planQty,
      versionId: estimateForm.versionId,
    },
  }
}

async function estimateCapacity() {
  if (estimateMode.value === 'order' && estimateForm.orderId <= 0) {
    ElMessage.warning('请输入生产订单 ID')
    return
  }
  if (
    estimateMode.value === 'temporary' &&
    (estimateForm.materialId <= 0 ||
      estimateForm.versionId <= 0 ||
      estimateForm.planQty <= 0 ||
      !estimateForm.expectedDate)
  ) {
    ElMessage.warning('请完整填写临时评估条件')
    return
  }
  estimateLoading.value = true
  estimateError.value = ''
  estimateResult.value = undefined
  estimateShortages.value = []
  try {
    const { request, shortage } = await resolveEstimateRequest()
    const [estimate, shortageResult] = await Promise.all([
      productionService.estimateCapacity(request satisfies ProductionCapacityEstimateFormData),
      inventoryService.calculateShortage([shortage]),
    ])
    estimateResult.value = estimate
    estimateShortages.value = shortageResult.items
  } catch (error) {
    estimateError.value = getErrorMessage(error, '交付能力评估失败')
  } finally {
    estimateLoading.value = false
  }
}

// ---------- 产能检测 ----------
const lineOptions = ref<ProductionLineItem[]>([])
const detectionLoading = ref(false)
const detectionError = ref('')
const detectionResult = ref<CapacityDetectionItem>()
const detectionForm = reactive({ lineId: 0, periodRange: [] as string[] })

async function loadLineOptions() {
  if (!canManageCapacity.value) {
    return
  }
  try {
    const result = await productionService.listLines({ page: 1, pageSize: 100 })
    lineOptions.value = result.items
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '生产线选项加载失败'))
  }
}

async function runDetection() {
  const [periodStart, periodEnd] = detectionForm.periodRange
  if (detectionForm.lineId <= 0 || !periodStart || !periodEnd) {
    ElMessage.warning('请选择生产线和完整统计周期')
    return
  }
  detectionLoading.value = true
  detectionError.value = ''
  try {
    detectionResult.value = await productionService.runCapacityDetection({
      lineId: detectionForm.lineId,
      periodEnd,
      periodStart,
    })
  } catch (error) {
    detectionResult.value = undefined
    detectionError.value = getErrorMessage(error, '产能检测失败')
  } finally {
    detectionLoading.value = false
  }
}

// ---------- 产能平衡 ----------
const balanceLoading = ref(false)
const balanceError = ref('')
const balanceResult = ref<CapacityBalanceItem>()
const balanceForm = reactive({
  affectedOrders: '',
  afterPlan: '',
  beforePlan: '',
})

function parseOrderIds(value: string) {
  return [
    ...new Set(
      value
        .split(/[\s,，]+/)
        .map((token) => Number(token))
        .filter((id) => Number.isInteger(id) && id > 0),
    ),
  ]
}

async function saveBalance() {
  const affectedOrders = parseOrderIds(balanceForm.affectedOrders)
  if (!affectedOrders.length) {
    ElMessage.warning('请至少填写一个受影响生产订单')
    return
  }
  balanceLoading.value = true
  balanceError.value = ''
  try {
    const beforePlan = JSON.parse(balanceForm.beforePlan) as Record<string, unknown>
    const afterPlan = JSON.parse(balanceForm.afterPlan) as Record<string, unknown>
    balanceResult.value = await productionService.saveCapacityBalance({
      affectedOrders,
      afterPlan,
      beforePlan,
    })
    ElMessage.success('产能平衡方案记录已保存')
  } catch (error) {
    balanceResult.value = undefined
    balanceError.value = getErrorMessage(error, '产能平衡保存失败，请检查 JSON 格式')
  } finally {
    balanceLoading.value = false
  }
}

// ---------- 生产线实时状态 ----------
const lineStatusLoading = ref(false)
const lineStatusError = ref('')
const lineStatusResult = ref<ProductionLineStatusItem>()
const lineStatusForm = reactive({
  currentMaterialId: 0,
  currentOrderId: 0,
  efficiency: undefined as number | undefined,
  finishedQty: undefined as number | undefined,
  lineId: 0,
  status: 'idle' as ProductionLineRunStatus,
})

async function updateLineStatus() {
  if (lineStatusForm.lineId <= 0) {
    ElMessage.warning('请选择生产线')
    return
  }
  if (
    lineStatusForm.efficiency !== undefined &&
    (lineStatusForm.efficiency < 0 || lineStatusForm.efficiency > 1)
  ) {
    ElMessage.warning('效率必须在 0 到 1 之间')
    return
  }
  lineStatusLoading.value = true
  lineStatusError.value = ''
  try {
    let currentMaterialId = lineStatusForm.currentMaterialId || undefined
    let currentOrderId = lineStatusForm.currentOrderId || undefined
    if (lineStatusForm.status === 'idle') {
      currentMaterialId = undefined
      currentOrderId = undefined
    }
    lineStatusResult.value = await productionService.updateLineStatus({
      currentMaterialId,
      currentOrderId,
      efficiency: lineStatusForm.efficiency,
      finishedQty: lineStatusForm.finishedQty,
      lineId: lineStatusForm.lineId,
      status: lineStatusForm.status,
    })
    ElMessage.success('生产线状态已更新')
    await loadLineOptions()
  } catch (error) {
    lineStatusResult.value = undefined
    lineStatusError.value = getErrorMessage(error, '生产线状态更新失败')
  } finally {
    lineStatusLoading.value = false
  }
}

onMounted(() => {
  if (
    route.query.tab === 'status' &&
    canManageCapacity.value &&
    typeof route.query.orderId === 'string'
  ) {
    const orderId = parsePositiveInt(route.query.orderId)
    if (orderId) {
      lineStatusForm.currentOrderId = orderId
      lineStatusForm.status = 'running'
    }
  }
  if (isExternalCustomer.value || canManageOrders.value) {
    void loadExternalOrders()
  }
  void loadLineOptions()
})
</script>

<template>
  <PageContainer>
    <PageHeader
      title="生产运营"
      description="处理外部订单、交付评估、产能检测与平衡，并维护生产线实时状态。"
    />

    <el-empty v-if="!hasOperationsAccess" description="当前账号暂无外部订单或生产运营权限" />
    <el-tabs v-else v-model="activeTab" class="operations-tabs">
      <el-tab-pane v-if="isExternalCustomer || canManageOrders" label="外部订单" name="external">
        <el-card class="section-card" shadow="never">
          <el-form :model="externalFilters" inline @submit.prevent="loadExternalOrders(1)">
            <el-form-item v-if="canManageOrders && !isExternalCustomer" label="客户 ID">
              <el-input
                v-model.trim="externalFilters.customerId"
                clearable
                placeholder="全部客户"
              />
            </el-form-item>
            <el-form-item label="订单状态">
              <el-select
                v-model="externalFilters.status"
                clearable
                placeholder="全部"
                style="width: 140px"
              >
                <el-option label="待审核" value="pending_review" />
                <el-option label="已接受" value="accepted" />
                <el-option label="已拒绝" value="rejected" />
              </el-select>
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :loading="externalLoading" @click="loadExternalOrders(1)">
                查询
              </el-button>
              <el-button :icon="Refresh" @click="resetExternalFilters">重置</el-button>
              <el-button type="primary" :icon="Plus" @click="openExternalCreate">
                {{ isExternalCustomer ? '提交订单' : '代录订单' }}
              </el-button>
            </el-form-item>
          </el-form>
        </el-card>

        <el-card class="section-card table-card" shadow="never">
          <el-alert
            v-if="externalError"
            class="request-error"
            :closable="false"
            show-icon
            :title="externalError"
            type="error"
          />
          <el-table v-else v-loading="externalLoading" :data="externalResult.items" stripe>
            <el-table-column label="订单号" min-width="90">
              <template #default="{ row }">#{{ row.extOrderId }}</template>
            </el-table-column>
            <el-table-column v-if="canManageOrders" label="客户" min-width="150">
              <template #default="{ row }">{{ row.customerName || `#${row.customerId}` }}</template>
            </el-table-column>
            <el-table-column label="产品" min-width="170">
              <template #default="{ row }">{{ row.materialName || `#${row.materialId}` }}</template>
            </el-table-column>
            <el-table-column label="数量" min-width="90">
              <template #default="{ row }">{{ formatNumber(row.quantity) }}</template>
            </el-table-column>
            <el-table-column label="期望日期" min-width="120" prop="expectedDate" />
            <el-table-column label="联系人" min-width="120" prop="contactPerson" />
            <el-table-column label="联系电话" min-width="140" prop="contactPhone" />
            <el-table-column label="状态" min-width="100">
              <template #default="{ row }">
                <StatusTag :labels="externalStatusLabels" :value="row.status" />
              </template>
            </el-table-column>
            <el-table-column label="提交时间" min-width="170">
              <template #default="{ row }">{{ formatDateTime(row.submitTime) }}</template>
            </el-table-column>
            <el-table-column label="审核意见" min-width="160">
              <template #default="{ row }">{{ row.reviewComment || '-' }}</template>
            </el-table-column>
            <el-table-column v-if="canManageOrders" fixed="right" label="操作" min-width="210">
              <template #default="{ row }">
                <template v-if="row.status === 'pending_review'">
                  <el-button link type="success" @click="reviewExternalOrder(row, true)">
                    接受
                  </el-button>
                  <el-button link type="danger" @click="reviewExternalOrder(row, false)">
                    拒绝
                  </el-button>
                </template>
                <el-button
                  v-if="row.status === 'accepted'"
                  link
                  type="primary"
                  @click="openConvert(row)"
                >
                  转生产订单
                </el-button>
              </template>
            </el-table-column>
          </el-table>
          <el-empty
            v-if="!externalLoading && !externalError && !externalResult.items.length"
            description="暂无外部订单"
          />
          <div v-if="externalResult.total > 0" class="pagination">
            <el-pagination
              v-model:current-page="externalPage"
              background
              layout="total, prev, pager, next"
              :page-size="externalPageSize"
              :total="externalResult.total"
              @current-change="loadExternalOrders"
            />
          </div>
          <el-alert
            v-if="convertResult"
            class="conversion-result"
            :closable="false"
            show-icon
            title="最近一次外部订单转换结果"
            type="success"
          >
            <template #default>
              <p>
                外部订单 #{{ convertResult.extOrderId }} 已生成
                {{
                  convertResult.productionOrders
                    .map((order) => `生产订单 #${order.orderId}`)
                    .join('、')
                }}
              </p>
              <p>
                关联记录：
                {{
                  convertResult.associations
                    .map(
                      (association) =>
                        `外部订单 #${association.extOrderId} → 生产订单 #${association.orderId}`,
                    )
                    .join('；')
                }}
              </p>
            </template>
          </el-alert>
        </el-card>
      </el-tab-pane>

      <el-tab-pane v-if="canManageCapacity" label="交付评估" name="estimate">
        <el-card class="section-card" shadow="never">
          <el-radio-group v-model="estimateMode" class="mode-switch">
            <el-radio-button value="order">按生产订单</el-radio-button>
            <el-radio-button value="temporary">临时评估</el-radio-button>
          </el-radio-group>
          <el-form :model="estimateForm" inline>
            <el-form-item v-if="estimateMode === 'order'" label="生产订单 ID">
              <el-input-number :controls="false" v-model="estimateForm.orderId" :min="1" />
            </el-form-item>
            <template v-else>
              <el-form-item label="产品物料 ID">
                <el-input-number :controls="false" v-model="estimateForm.materialId" :min="1" />
              </el-form-item>
              <el-form-item label="BOM 版本 ID">
                <el-input-number :controls="false" v-model="estimateForm.versionId" :min="1" />
              </el-form-item>
              <el-form-item label="计划数量">
                <el-input-number :controls="false" v-model="estimateForm.planQty" :min="1" />
              </el-form-item>
              <el-form-item label="期望日期">
                <el-date-picker
                  v-model="estimateForm.expectedDate"
                  type="date"
                  value-format="YYYY-MM-DD"
                />
              </el-form-item>
            </template>
            <el-form-item>
              <el-button
                :icon="Search"
                :loading="estimateLoading"
                type="primary"
                @click="estimateCapacity"
              >
                开始评估
              </el-button>
            </el-form-item>
          </el-form>
        </el-card>
        <el-card v-loading="estimateLoading" class="section-card table-card" shadow="never">
          <el-alert v-if="estimateError" :closable="false" :title="estimateError" type="error" />
          <template v-else-if="estimateResult">
            <div class="metric-grid">
              <div class="metric">
                <span>按期交付</span>
                <el-tag :type="estimateResult.canDeliverOnTime ? 'success' : 'danger'">
                  {{ estimateResult.canDeliverOnTime ? '可以' : '存在风险' }}
                </el-tag>
              </div>
              <div class="metric">
                <span>物料齐套</span>
                <strong>{{ estimateResult.materialReady ? '是' : '否' }}</strong>
              </div>
              <div class="metric">
                <span>产能满足</span>
                <strong>{{ estimateResult.capacityReady ? '是' : '否' }}</strong>
              </div>
              <div class="metric">
                <span>预计完工</span>
                <strong>{{ estimateResult.estimatedFinishDate || '-' }}</strong>
              </div>
              <div class="metric">
                <span>所需工时</span>
                <strong>{{ formatNumber(estimateResult.requiredWorkMinutes) }} 分钟</strong>
              </div>
              <div class="metric">
                <span>可用工时</span>
                <strong>{{ formatNumber(estimateResult.availableWorkMinutes) }} 分钟</strong>
              </div>
            </div>
            <el-alert
              v-if="estimateResult.riskReason"
              :closable="false"
              show-icon
              :title="estimateResult.riskReason"
              type="warning"
            />
            <h3 class="section-title">物料齐套明细</h3>
            <el-table :data="estimateShortages" stripe>
              <el-table-column label="层级" min-width="80" prop="level" />
              <el-table-column label="物料" min-width="180">
                <template #default="{ row }">
                  {{ row.materialName || `#${row.materialId}` }}
                </template>
              </el-table-column>
              <el-table-column label="毛需求" min-width="100">
                <template #default="{ row }">{{ formatNumber(row.grossRequirement) }}</template>
              </el-table-column>
              <el-table-column label="可用库存" min-width="100">
                <template #default="{ row }">{{ formatNumber(row.availableQty) }}</template>
              </el-table-column>
              <el-table-column label="在途数量" min-width="100">
                <template #default="{ row }">{{ formatNumber(row.inTransitQty) }}</template>
              </el-table-column>
              <el-table-column label="净缺口" min-width="100">
                <template #default="{ row }">
                  <el-tag :type="row.netShortageQty > 0 ? 'danger' : 'success'">
                    {{ formatNumber(row.netShortageQty) }}
                  </el-tag>
                </template>
              </el-table-column>
              <el-table-column label="建议采购" min-width="100">
                <template #default="{ row }">{{ formatNumber(row.suggestedPurchaseQty) }}</template>
              </el-table-column>
            </el-table>
            <el-empty
              v-if="!estimateShortages.length"
              :image-size="60"
              description="当前评估未返回物料缺口明细"
            />
          </template>
          <el-empty v-else description="填写条件后开始评估交付能力" />
        </el-card>
      </el-tab-pane>

      <el-tab-pane v-if="canManageCapacity" label="产能检测" name="detection">
        <el-card class="section-card" shadow="never">
          <el-form :model="detectionForm" inline>
            <el-form-item label="生产线">
              <el-select v-model="detectionForm.lineId" filterable style="width: 200px">
                <el-option
                  v-for="line in lineOptions"
                  :key="line.lineId"
                  :label="`生产线 #${line.lineId} · ${line.typeName || '-'}`"
                  :value="line.lineId"
                />
              </el-select>
            </el-form-item>
            <el-form-item label="统计周期">
              <el-date-picker
                v-model="detectionForm.periodRange"
                end-placeholder="结束时间"
                range-separator="至"
                start-placeholder="开始时间"
                type="datetimerange"
                value-format="YYYY-MM-DDTHH:mm:ss"
              />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :loading="detectionLoading" @click="runDetection">
                执行检测
              </el-button>
            </el-form-item>
          </el-form>
        </el-card>
        <el-card v-loading="detectionLoading" class="section-card" shadow="never">
          <el-alert v-if="detectionError" :closable="false" :title="detectionError" type="error" />
          <template v-else-if="detectionResult">
            <div class="metric-grid">
              <div class="metric">
                <span>计划产能</span>
                <strong>{{ formatNumber(detectionResult.planCapacity) }}</strong>
              </div>
              <div class="metric">
                <span>实际产能</span>
                <strong>{{ formatNumber(detectionResult.actualCapacity) }}</strong>
              </div>
              <div class="metric">
                <span>差异数量</span>
                <strong>{{ formatNumber(detectionResult.diffQty) }}</strong>
              </div>
              <div class="metric">
                <span>生产效率</span>
                <strong>{{
                  detectionResult.efficiency === undefined
                    ? '-'
                    : `${formatNumber(detectionResult.efficiency * 100)}%`
                }}</strong>
              </div>
              <div class="metric">
                <span>实际工时</span>
                <strong>{{ formatNumber(detectionResult.actualWorkHours) }} 小时</strong>
              </div>
              <div class="metric">
                <span>停机时长</span>
                <strong>{{ formatNumber(detectionResult.downtimeMinutes) }} 分钟</strong>
              </div>
            </div>
            <el-progress
              v-if="detectionResult.efficiency !== undefined"
              :percentage="Math.min(100, Math.round(detectionResult.efficiency * 100))"
              :status="detectionResult.efficiency >= 0.8 ? 'success' : 'warning'"
            />
          </template>
          <el-empty v-else description="选择生产线与统计周期后执行产能检测" />
        </el-card>
      </el-tab-pane>

      <el-tab-pane v-if="canManageCapacity" label="产能平衡" name="balance">
        <el-card class="section-card" shadow="never">
          <p>保存调整方案及关联订单；订单计划与生产日历在对应页面维护。</p>
          <el-form :model="balanceForm" label-position="top">
            <el-form-item label="受影响生产订单">
              <el-input
                v-model.trim="balanceForm.affectedOrders"
                placeholder="多个订单 ID 用逗号分隔"
              />
            </el-form-item>
            <div class="plan-grid">
              <el-form-item label="调整前方案（JSON）">
                <el-input v-model="balanceForm.beforePlan" :rows="8" type="textarea" />
              </el-form-item>
              <el-form-item label="调整后方案（JSON）">
                <el-input v-model="balanceForm.afterPlan" :rows="8" type="textarea" />
              </el-form-item>
            </div>
            <el-button type="primary" :loading="balanceLoading" @click="saveBalance">
              保存方案记录
            </el-button>
          </el-form>
        </el-card>
        <el-card class="section-card" shadow="never">
          <el-alert v-if="balanceError" :closable="false" :title="balanceError" type="error" />
          <el-descriptions v-else-if="balanceResult" border :column="2">
            <el-descriptions-item label="调整记录"
              >#{{ balanceResult.balanceId }}</el-descriptions-item
            >
            <el-descriptions-item label="调整时间">
              {{ formatDateTime(balanceResult.adjustTime) }}
            </el-descriptions-item>
            <el-descriptions-item label="调整人"
              >#{{ balanceResult.operatorId }}</el-descriptions-item
            >
            <el-descriptions-item label="受影响订单">
              {{ balanceResult.affectedOrders.map((id) => `#${id}`).join('、') }}
            </el-descriptions-item>
            <el-descriptions-item label="调整前">
              <pre>{{ JSON.stringify(balanceResult.beforePlan, null, 2) }}</pre>
            </el-descriptions-item>
            <el-descriptions-item label="调整后">
              <pre>{{ JSON.stringify(balanceResult.afterPlan, null, 2) }}</pre>
            </el-descriptions-item>
          </el-descriptions>
          <el-empty v-else description="保存调整后显示前后方案与受影响订单" />
        </el-card>
      </el-tab-pane>

      <el-tab-pane v-if="canManageCapacity" label="产线状态与报工" name="status">
        <el-card class="section-card table-card" shadow="never">
          <el-table :data="lineOptions" stripe>
            <el-table-column label="生产线" min-width="100">
              <template #default="{ row }">#{{ row.lineId }}</template>
            </el-table-column>
            <el-table-column label="线型" min-width="150">
              <template #default="{ row }">{{ row.typeName || `#${row.typeId}` }}</template>
            </el-table-column>
            <el-table-column label="负责人" min-width="130">
              <template #default="{ row }">{{ row.managerName || `#${row.managerId}` }}</template>
            </el-table-column>
            <el-table-column label="启用日期" min-width="120" prop="startDate" />
            <el-table-column label="当前状态" min-width="110">
              <template #default="{ row }">
                <StatusTag v-if="row.status" :labels="lineStatusLabels" :value="row.status" />
                <span v-else>-</span>
              </template>
            </el-table-column>
          </el-table>
          <el-empty v-if="!lineOptions.length" description="暂无生产线数据" />
        </el-card>
        <el-card class="section-card" shadow="never">
          <p>按产线当前任务记录累计产量；订单完工数量在生产订单页登记。</p>
          <el-form :model="lineStatusForm" inline>
            <el-form-item label="生产线">
              <el-select v-model="lineStatusForm.lineId" filterable style="width: 180px">
                <el-option
                  v-for="line in lineOptions"
                  :key="line.lineId"
                  :label="`生产线 #${line.lineId}`"
                  :value="line.lineId"
                />
              </el-select>
            </el-form-item>
            <el-form-item label="运行状态">
              <el-select v-model="lineStatusForm.status" style="width: 120px">
                <el-option label="空闲" value="idle" />
                <el-option label="运行中" value="running" />
                <el-option label="故障" value="fault" />
              </el-select>
            </el-form-item>
            <el-form-item v-if="lineStatusForm.status !== 'idle'" label="当前订单">
              <el-input-number :controls="false" v-model="lineStatusForm.currentOrderId" :min="0" />
            </el-form-item>
            <el-form-item v-if="lineStatusForm.status !== 'idle'" label="当前产品">
              <el-input-number
                :controls="false"
                v-model="lineStatusForm.currentMaterialId"
                :min="0"
              />
            </el-form-item>
            <el-form-item label="累计完成数量">
              <el-input-number
                :controls="false"
                v-model="lineStatusForm.finishedQty"
                :min="0"
                placeholder="留空由后端沿用当前任务数量"
              />
            </el-form-item>
            <el-form-item label="当前效率">
              <el-input-number
                :controls="false"
                v-model="lineStatusForm.efficiency"
                :max="1"
                :min="0"
                :precision="2"
                :step="0.05"
                placeholder="留空保留当前效率"
              />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :loading="lineStatusLoading" @click="updateLineStatus">
                更新状态
              </el-button>
            </el-form-item>
          </el-form>
        </el-card>
        <el-card class="section-card" shadow="never">
          <el-alert
            v-if="lineStatusError"
            :closable="false"
            :title="lineStatusError"
            type="error"
          />
          <el-descriptions v-else-if="lineStatusResult" border :column="3">
            <el-descriptions-item label="生产线"
              >#{{ lineStatusResult.lineId }}</el-descriptions-item
            >
            <el-descriptions-item label="状态">
              <StatusTag :labels="lineStatusLabels" :value="lineStatusResult.status" />
            </el-descriptions-item>
            <el-descriptions-item label="更新时间">
              {{ formatDateTime(lineStatusResult.updatedTime) }}
            </el-descriptions-item>
            <el-descriptions-item label="当前订单">
              {{ lineStatusResult.currentOrderId ? `#${lineStatusResult.currentOrderId}` : '-' }}
            </el-descriptions-item>
            <el-descriptions-item label="当前产品">
              {{
                lineStatusResult.currentMaterialId ? `#${lineStatusResult.currentMaterialId}` : '-'
              }}
            </el-descriptions-item>
            <el-descriptions-item label="已完成数量">
              {{ formatNumber(lineStatusResult.finishedQty) }}
            </el-descriptions-item>
            <el-descriptions-item label="当前效率">
              {{ formatNumber(lineStatusResult.efficiency * 100) }}%
            </el-descriptions-item>
          </el-descriptions>
          <el-empty v-else description="提交状态后显示生产线实时详情" />
        </el-card>
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="externalCreateVisible" title="提交外部订单" width="540px">
      <el-form :model="externalForm" label-width="110px">
        <el-form-item v-if="canManageOrders && !isExternalCustomer" label="客户 ID">
          <el-input-number
            :controls="false"
            v-model="externalForm.customerId"
            :min="1"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="产品物料 ID">
          <el-input-number
            :controls="false"
            v-model="externalForm.materialId"
            :min="1"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="数量">
          <el-input-number
            :controls="false"
            v-model="externalForm.quantity"
            :min="1"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="期望日期">
          <el-date-picker
            v-model="externalForm.expectedDate"
            style="width: 100%"
            type="date"
            value-format="YYYY-MM-DD"
          />
        </el-form-item>
        <el-form-item label="联系人">
          <el-input v-model.trim="externalForm.contactPerson" />
        </el-form-item>
        <el-form-item label="联系电话">
          <el-input v-model.trim="externalForm.contactPhone" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="externalCreateVisible = false">取消</el-button>
        <el-button type="primary" :loading="externalSubmitting" @click="submitExternalOrder">
          提交
        </el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="convertVisible" title="转换为生产订单" width="560px">
      <el-form :model="convertForm" label-width="120px">
        <el-form-item label="产品物料 ID">
          <el-input-number
            :controls="false"
            v-model="convertForm.materialId"
            :min="1"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="BOM 版本 ID">
          <el-input-number
            :controls="false"
            v-model="convertForm.versionId"
            :min="1"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="计划数量">
          <el-input-number
            :controls="false"
            v-model="convertForm.planQty"
            :min="1"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="计划开工">
          <el-date-picker
            v-model="convertForm.planStart"
            style="width: 100%"
            type="date"
            value-format="YYYY-MM-DD"
          />
        </el-form-item>
        <el-form-item label="计划完工">
          <el-date-picker
            v-model="convertForm.planEnd"
            style="width: 100%"
            type="date"
            value-format="YYYY-MM-DD"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="convertVisible = false">取消</el-button>
        <el-button type="primary" :loading="convertSubmitting" @click="submitConvert">
          确认转换
        </el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.operations-tabs,
.section-card {
  min-width: 0;
}
.section-card {
  margin-bottom: 16px;
}
.request-error,
.mode-switch {
  margin-bottom: 16px;
}
.conversion-result {
  margin-top: 16px;
}
.conversion-result p {
  margin: 4px 0;
}
.section-title {
  margin: 20px 0 12px;
  font-size: 16px;
}
.pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
.metric-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 14px;
  margin-bottom: 16px;
}
.metric {
  display: flex;
  min-height: 86px;
  flex-direction: column;
  justify-content: space-between;
  padding: 16px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 8px;
  background: var(--el-fill-color-light);
}
.metric span {
  color: var(--el-text-color-secondary);
}
.metric strong {
  font-size: 20px;
}
.plan-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
}
pre {
  margin: 0;
  white-space: pre-wrap;
}
@media (max-width: 900px) {
  .metric-grid,
  .plan-grid {
    grid-template-columns: 1fr;
  }
}
</style>
