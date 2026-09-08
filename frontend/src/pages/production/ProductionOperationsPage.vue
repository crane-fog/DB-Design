<script setup lang="ts">
import {
  type ExternalOrderConvertItem,
  type ExternalOrderFormOptionsItem,
  type ExternalOrderItem,
  type ProductionOrderProductOption,
  productionService,
} from '@/services/ProductionService'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Refresh } from '@element-plus/icons-vue'
import { computed, onMounted, reactive, ref } from 'vue'
import { productionOrderStatusLabels } from '@/constants/status'
import { formatDateTime, formatNumber } from '@/utils/format'
import { PermissionCode } from '@/constants/permissions'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import type { PageResult } from '@/services/pagination'
import StatusTag from '@/components/common/StatusTag.vue'
import { getErrorMessage } from '@/utils/error'
import { useAuthStore } from '@/stores/auth'

const externalStatusLabels = {
  accepted: '已接受',
  converted: '已转换',
  delivered: '已交货',
  pending_review: '待审核',
  rejected: '已拒绝',
}
const auth = useAuthStore()
const canViewOwnExternal = computed(() => auth.hasPermission(PermissionCode.ExternalOrderViewOwn))
const canViewAllExternal = computed(() => auth.hasPermission(PermissionCode.ExternalOrderViewAll))
const canViewExternal = computed(() => canViewOwnExternal.value || canViewAllExternal.value)
const canCreateOwnExternal = computed(() =>
  auth.hasPermission(PermissionCode.ExternalOrderCreateOwn),
)
const canCreateExternalForCustomer = computed(() =>
  auth.hasPermission(PermissionCode.ExternalOrderCreateForCustomer),
)
const canCreateExternal = computed(
  () => canCreateOwnExternal.value || canCreateExternalForCustomer.value,
)
const canReviewExternal = computed(() => auth.hasPermission(PermissionCode.ExternalOrderReview))
const canConvertExternal = computed(() => auth.hasPermission(PermissionCode.ExternalOrderConvert))
const hasExternalAccess = computed(() => canViewExternal.value || canCreateExternal.value)

// ---------- 外部订单 ----------
const externalPageSize = 10
const externalPage = ref(1)
const externalLoading = ref(false)
const externalError = ref('')
const externalFilters = reactive({ customerName: '', status: '' })
const externalResult = ref<PageResult<ExternalOrderItem>>({
  items: [],
  page: 1,
  pageSize: externalPageSize,
  total: 0,
})
const externalCreateVisible = ref(false)
const externalSubmitting = ref(false)
const externalFormOptions = ref<ExternalOrderFormOptionsItem>({ customers: [], materials: [] })
const externalFormOptionsError = ref('')
const externalFormOptionsLoading = ref(false)
let externalFormOptionsLoaded = false
let externalFormOptionsPromise: Promise<void> | undefined = undefined
const deliverySubmittingId = ref<number>()
const externalForm = reactive({
  contactPerson: '',
  contactPhone: '',
  customerId: undefined as number | undefined,
  expectedDate: '',
  materialId: undefined as number | undefined,
  quantity: 1,
})

const convertVisible = ref(false)
const convertingOrder = ref<ExternalOrderItem>()
const convertResult = ref<ExternalOrderConvertItem>()
const convertSubmitting = ref(false)
const convertProductOptions = ref<ProductionOrderProductOption[]>([])
const convertProductOptionsLoading = ref(false)
const convertProductOptionsError = ref('')
let convertProductOptionsLoaded = false
let convertProductOptionsPromise: Promise<void> | undefined = undefined
const convertForm = reactive({
  materialId: 0,
  planEnd: '',
  planQty: 1,
  planStart: '',
  versionId: 0,
})

function selectedExternalStatus() {
  const { status } = externalFilters
  if (
    status === 'accepted' ||
    status === 'converted' ||
    status === 'delivered' ||
    status === 'pending_review' ||
    status === 'rejected'
  ) {
    return status
  }
  return undefined
}

async function loadExternalOrders(targetPage = externalPage.value) {
  if (!canViewExternal.value) {
    return
  }
  externalLoading.value = true
  externalError.value = ''
  try {
    let customerName = undefined as string | undefined
    if (canViewAllExternal.value) {
      customerName = externalFilters.customerName.trim() || undefined
    }
    externalResult.value = await productionService.listExternalOrders({
      customerName,
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
  Object.assign(externalFilters, { customerName: '', status: '' })
  void loadExternalOrders(1)
}

function normalizeExternalFormSelections() {
  if (
    !externalFormOptions.value.materials.some(
      (material) => material.materialId === externalForm.materialId,
    )
  ) {
    externalForm.materialId = undefined
  }
  if (
    !externalFormOptions.value.customers.some(
      (customer) => customer.userId === externalForm.customerId,
    )
  ) {
    externalForm.customerId = undefined
  }
}

async function loadExternalFormOptions(force = false) {
  if (externalFormOptionsPromise) {
    await externalFormOptionsPromise
    return
  }
  if (externalFormOptionsLoaded && !force) {
    return
  }
  externalFormOptionsPromise = (async () => {
    externalFormOptionsLoading.value = true
    externalFormOptionsError.value = ''
    try {
      externalFormOptions.value = await productionService.listExternalOrderFormOptions()
      externalFormOptionsLoaded = true
      normalizeExternalFormSelections()
    } catch (error) {
      externalFormOptions.value = { customers: [], materials: [] }
      externalFormOptionsLoaded = false
      externalFormOptionsError.value = getErrorMessage(error, '外部订单表单选项加载失败')
    } finally {
      externalFormOptionsLoading.value = false
    }
  })()
  try {
    await externalFormOptionsPromise
  } finally {
    externalFormOptionsPromise = undefined
  }
}

async function openExternalCreate() {
  Object.assign(externalForm, {
    contactPerson: '',
    contactPhone: '',
    customerId: undefined,
    expectedDate: '',
    materialId: undefined,
    quantity: 1,
  })
  externalCreateVisible.value = true
  await loadExternalFormOptions()
}

async function submitExternalOrder() {
  if (
    !externalForm.materialId ||
    externalForm.quantity <= 0 ||
    !externalForm.expectedDate ||
    !externalForm.contactPerson.trim() ||
    !externalForm.contactPhone.trim()
  ) {
    ElMessage.warning('请完整填写产品、数量、日期和联系方式')
    return
  }
  if (canCreateExternalForCustomer.value && !externalForm.customerId) {
    ElMessage.warning('请选择外部客户')
    return
  }
  externalSubmitting.value = true
  try {
    let customerId = undefined as number | undefined
    if (canCreateExternalForCustomer.value && externalForm.customerId) {
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

async function loadConvertProductOptions(force = false) {
  if (convertProductOptionsPromise) {
    await convertProductOptionsPromise
    return
  }
  if (convertProductOptionsLoaded && !force) {
    return
  }
  convertProductOptionsPromise = (async () => {
    convertProductOptionsLoading.value = true
    convertProductOptionsError.value = ''
    try {
      convertProductOptions.value = await productionService.listOrderProductOptions()
      convertProductOptionsLoaded = true
    } catch (error) {
      convertProductOptions.value = []
      convertProductOptionsLoaded = false
      convertProductOptionsError.value = getErrorMessage(error, '产品与 BOM 版本加载失败')
    } finally {
      convertProductOptionsLoading.value = false
    }
  })()
  try {
    await convertProductOptionsPromise
  } finally {
    convertProductOptionsPromise = undefined
  }
}

const currentConvertProductOptions = computed(() => {
  const materialId = convertingOrder.value?.materialId
  return convertProductOptions.value.filter((option) => option.materialId === materialId)
})

function selectCurrentConvertProduct(order: ExternalOrderItem) {
  const option = convertProductOptions.value.find((item) => item.materialId === order.materialId)
  convertForm.materialId = option?.materialId ?? order.materialId
  convertForm.versionId = option?.versionId ?? 0
}

async function reloadConvertProductOptions() {
  await loadConvertProductOptions(true)
  if (convertingOrder.value) {
    selectCurrentConvertProduct(convertingOrder.value)
  }
}

async function openConvert(order: ExternalOrderItem) {
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
  await loadConvertProductOptions()
  if (!convertVisible.value || convertingOrder.value?.extOrderId !== order.extOrderId) {
    return
  }
  selectCurrentConvertProduct(order)
}

async function submitConvert() {
  const selectedProduct = currentConvertProductOptions.value.find(
    (option) => option.versionId === convertForm.versionId,
  )
  if (
    !convertingOrder.value ||
    !selectedProduct ||
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
      productionOrders: [
        {
          ...convertForm,
          materialId: selectedProduct.materialId,
          versionId: selectedProduct.versionId,
        },
      ],
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

async function deliverExternalOrder(order: ExternalOrderItem) {
  if (!canConvertExternal.value || !order.deliveryReady || deliverySubmittingId.value) {
    return
  }
  try {
    await ElMessageBox.confirm(
      `确定整单交货外部订单 #${order.extOrderId} 的 ${order.materialName || `物料 #${order.materialId}`} × ${formatNumber(order.quantity)} 吗？交货后将扣减成品库存，且不可重复交货。`,
      '确认整单交货',
      {
        cancelButtonText: '取消',
        confirmButtonText: '确认交货',
        type: 'warning',
      },
    )
    deliverySubmittingId.value = order.extOrderId
    const result = await productionService.deliverExternalOrder(order.extOrderId)
    ElMessage.success(
      `外部订单 #${order.extOrderId} 已交货，成品剩余可用库存 ${formatNumber(result.remainingAvailableQty)}`,
    )
    await loadExternalOrders(externalPage.value)
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') {
      ElMessage.error(getErrorMessage(error, '外部订单交货失败'))
    }
  } finally {
    deliverySubmittingId.value = undefined
  }
}

onMounted(() => {
  if (canViewExternal.value) {
    void loadExternalOrders()
  }
})
</script>

<template>
  <PageContainer>
    <PageHeader title="外部订单" description="提交、审核、转换并交付外部订单。" />

    <el-empty v-if="!hasExternalAccess" description="当前账号暂无外部订单权限" />
    <template v-else>
      <el-card class="section-card" shadow="never">
        <el-form :model="externalFilters" inline @submit.prevent="loadExternalOrders(1)">
          <el-form-item v-if="canViewAllExternal" label="客户名">
            <el-input
              v-model.trim="externalFilters.customerName"
              clearable
              placeholder="输入客户名模糊查询"
            />
          </el-form-item>
          <el-form-item v-if="canViewExternal" label="订单状态">
            <el-select
              v-model="externalFilters.status"
              clearable
              placeholder="全部"
              style="width: 140px"
            >
              <el-option label="待审核" value="pending_review" />
              <el-option label="已接受" value="accepted" />
              <el-option label="已转换" value="converted" />
              <el-option label="已交货" value="delivered" />
              <el-option label="已拒绝" value="rejected" />
            </el-select>
          </el-form-item>
          <el-form-item>
            <el-button
              v-if="canViewExternal"
              type="primary"
              :loading="externalLoading"
              @click="loadExternalOrders(1)"
            >
              查询
            </el-button>
            <el-button v-if="canViewExternal" :icon="Refresh" @click="resetExternalFilters">
              重置
            </el-button>
            <el-button
              v-if="canCreateExternal"
              type="primary"
              :icon="Plus"
              @click="openExternalCreate"
            >
              提交订单
            </el-button>
          </el-form-item>
        </el-form>
      </el-card>

      <el-card v-if="canViewExternal" class="section-card table-card" shadow="never">
        <el-alert
          v-if="externalError"
          class="request-error"
          :closable="false"
          show-icon
          :title="externalError"
          type="error"
        />
        <el-table v-else v-loading="externalLoading" :data="externalResult.items" stripe>
          <el-table-column label="外部订单 ID" min-width="100">
            <template #default="{ row }">{{ row.extOrderId }}</template>
          </el-table-column>
          <el-table-column v-if="canViewAllExternal" label="客户" min-width="120">
            <template #default="{ row }">{{ row.customerName || `#${row.customerId}` }}</template>
          </el-table-column>
          <el-table-column label="产品" min-width="100">
            <template #default="{ row }">{{ row.materialName || `#${row.materialId}` }}</template>
          </el-table-column>
          <el-table-column label="数量" min-width="60">
            <template #default="{ row }">{{ formatNumber(row.quantity) }}</template>
          </el-table-column>
          <el-table-column label="期望日期" min-width="100" prop="expectedDate" />
          <el-table-column label="联系人" min-width="70" prop="contactPerson" />
          <el-table-column label="联系电话" min-width="110" prop="contactPhone" />
          <el-table-column label="状态" min-width="80">
            <template #default="{ row }">
              <StatusTag :labels="externalStatusLabels" :value="row.status" />
            </template>
          </el-table-column>
          <el-table-column label="关联生产订单" min-width="120">
            <template #default="{ row }">
              <div v-if="row.productionOrders.length" class="linked-orders">
                <div
                  v-for="productionOrder in row.productionOrders"
                  :key="productionOrder.orderId"
                  class="linked-order"
                >
                  <p>#{{ productionOrder.orderId }}</p>
                  <StatusTag
                    :labels="productionOrderStatusLabels"
                    :value="productionOrder.status"
                  />
                </div>
              </div>
              <span v-else>-</span>
            </template>
          </el-table-column>
          <el-table-column label="提交时间" min-width="140">
            <template #default="{ row }">{{ formatDateTime(row.submitTime) }}</template>
          </el-table-column>
          <el-table-column label="审核意见" min-width="140">
            <template #default="{ row }">{{ row.reviewComment || '-' }}</template>
          </el-table-column>
          <el-table-column
            v-if="canReviewExternal || canConvertExternal"
            fixed="right"
            label="操作"
            min-width="120"
          >
            <template #default="{ row }">
              <template v-if="canReviewExternal && row.status === 'pending_review'">
                <el-button link type="success" @click="reviewExternalOrder(row, true)">
                  接受
                </el-button>
                <el-button link type="danger" @click="reviewExternalOrder(row, false)">
                  拒绝
                </el-button>
              </template>
              <el-button
                v-if="canConvertExternal && row.status === 'accepted'"
                link
                type="primary"
                @click="openConvert(row)"
              >
                转生产订单
              </el-button>
              <el-button
                v-if="canConvertExternal && row.deliveryReady"
                link
                :loading="deliverySubmittingId === row.extOrderId"
                type="success"
                @click="deliverExternalOrder(row)"
              >
                交货
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
    </template>

    <el-dialog v-model="externalCreateVisible" title="提交外部订单" width="540px">
      <el-alert
        v-if="externalFormOptionsError"
        class="request-error"
        :closable="false"
        show-icon
        :title="externalFormOptionsError"
        type="error"
      >
        <template #default>
          <el-button link type="primary" @click="loadExternalFormOptions(true)">
            重新加载选项
          </el-button>
        </template>
      </el-alert>
      <el-alert
        v-else-if="!externalFormOptionsLoading && !externalFormOptions.materials.length"
        class="request-error"
        :closable="false"
        show-icon
        title="暂无可下单的成品物料"
        type="warning"
      />
      <el-alert
        v-else-if="
          canCreateExternalForCustomer &&
          !externalFormOptionsLoading &&
          !externalFormOptions.customers.length
        "
        class="request-error"
        :closable="false"
        show-icon
        title="暂无可选择的外部客户"
        type="warning"
      />
      <el-form :model="externalForm" label-width="110px">
        <el-form-item v-if="canCreateExternalForCustomer" label="外部客户">
          <el-select
            v-model="externalForm.customerId"
            clearable
            filterable
            :loading="externalFormOptionsLoading"
            no-data-text="暂无可选外部客户"
            placeholder="请选择外部客户"
            style="width: 100%"
          >
            <el-option
              v-for="customer in externalFormOptions.customers"
              :key="customer.userId"
              :label="`${customer.userName} · ${customer.employeeNo}`"
              :value="customer.userId"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="产品物料">
          <el-select
            v-model="externalForm.materialId"
            clearable
            filterable
            :loading="externalFormOptionsLoading"
            no-data-text="暂无可下单的成品物料"
            placeholder="请选择成品物料"
            style="width: 100%"
          >
            <el-option
              v-for="material in externalFormOptions.materials"
              :key="material.materialId"
              :label="`${material.materialName} · ${material.model} · #${material.materialId}`"
              :value="material.materialId"
            />
          </el-select>
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
        <el-button
          type="primary"
          :disabled="
            externalFormOptionsLoading ||
            Boolean(externalFormOptionsError) ||
            !externalFormOptions.materials.length ||
            (canCreateExternalForCustomer && !externalFormOptions.customers.length)
          "
          :loading="externalSubmitting"
          @click="submitExternalOrder"
        >
          提交
        </el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="convertVisible" title="转换为生产订单" width="560px">
      <el-alert
        v-if="convertProductOptionsError"
        class="request-error"
        :closable="false"
        show-icon
        :title="convertProductOptionsError"
        type="error"
      >
        <template #default>
          <el-button link type="primary" @click="reloadConvertProductOptions">
            重新加载选项
          </el-button>
        </template>
      </el-alert>
      <el-alert
        v-else-if="
          !convertProductOptionsLoading &&
          convertingOrder &&
          currentConvertProductOptions.length === 0
        "
        class="request-error"
        :closable="false"
        show-icon
        :title="`${convertingOrder.materialName || `物料 #${convertingOrder.materialId}`}没有当前生效的 BOM 版本，暂时无法转换`"
        type="warning"
      />
      <el-form :model="convertForm" label-width="120px">
        <el-form-item label="产品">
          <el-select
            v-model="convertForm.versionId"
            :loading="convertProductOptionsLoading"
            no-data-text="该产品暂无当前生效的 BOM 版本"
            placeholder="请选择当前生效的产品 BOM"
            style="width: 100%"
          >
            <el-option
              v-for="option in currentConvertProductOptions"
              :key="option.versionId"
              :label="`${option.materialName} ${option.versionNo} #${option.materialId}`"
              :value="option.versionId"
            />
          </el-select>
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
        <el-button
          type="primary"
          :disabled="convertProductOptionsLoading || currentConvertProductOptions.length === 0"
          :loading="convertSubmitting"
          @click="submitConvert"
        >
          确认转换
        </el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.section-card {
  min-width: 0;
  margin-bottom: 16px;
}
.request-error {
  margin-bottom: 16px;
}
.conversion-result {
  margin-top: 16px;
}
.conversion-result p {
  margin: 4px 0;
}
.linked-orders {
  display: grid;
  gap: 6px;
}
.linked-order {
  display: grid;
  grid-template-columns: auto auto 1fr;
  align-items: center;
  gap: 8px;
}
.linked-order p {
  margin: 0;
}
.pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
</style>
