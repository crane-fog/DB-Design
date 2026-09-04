<script setup lang="ts">
import type {
  CompletionInboundDetail,
  CompletionInboundFormData,
  CompletionInboundItem,
  CompletionInboundQuery,
  InventoryProductionOrderOption,
  InventoryReferenceData,
} from '@/types/inventory'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { Plus, Refresh, Search } from '@element-plus/icons-vue'
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { formatDateTime, formatNumber } from '@/utils/format'
import EmptyState from '@/components/common/EmptyState.vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import { getErrorMessage } from '@/utils/error'
import { inventoryService } from '@/services/InventoryService'
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'

const auth = useAuthStore()
const router = useRouter()
const loading = ref(false)
const submitting = ref(false)
const error = ref('')
const items = ref<CompletionInboundItem[]>([])
const total = ref(0)
const dateRange = ref<[string, string]>()
const query = reactive<CompletionInboundQuery>({ page: 1, pageSize: 10 })
const dialogOpen = ref(false)
const detailDrawerOpen = ref(false)
const detailLoading = ref(false)
const detailError = ref('')
const selectedInbound = ref<CompletionInboundDetail>()
const formRef = ref<FormInstance>()
const form = reactive<CompletionInboundFormData>({
  batchNo: '',
  finishQty: 1,
  materialId: 0,
  operatorId: 0,
  orderId: 0,
  qualifiedQty: 1,
  versionId: 0,
})
let alive = true
let requestId = 0
const referenceLoading = ref(false)
const referenceError = ref('')
const referenceData = ref<InventoryReferenceData>({
  bomVersions: [],
  materials: [],
  productionOrders: [],
})
let referenceRequestId = 0

const qualifiedRate = computed(() => calculateQualifiedRate(form.finishQty, form.qualifiedQty))
const selectedProductionOrder = computed(() =>
  referenceData.value.productionOrders.find((item) => item.orderId === form.orderId),
)
const inboundOrderOptions = computed(() =>
  referenceData.value.productionOrders.filter(
    (item) => item.remainingQty > 0 && !['cancelled', 'completed'].includes(item.status),
  ),
)

function calculateQualifiedRate(finishQty: number, qualifiedQty: number) {
  if (finishQty <= 0) {
    return 0
  }
  return (qualifiedQty / finishQty) * 100
}

const formRules: FormRules<CompletionInboundFormData> = {
  batchNo: [
    { message: '请输入生产批次号', required: true, trigger: 'blur' },
    { max: 80, message: '批次号不能超过 80 个字符', trigger: 'blur' },
  ],
  finishQty: [
    { message: '请输入完工数量', required: true, trigger: 'blur', type: 'number' },
    { message: '完工数量必须大于 0', min: 0.01, trigger: 'blur', type: 'number' },
  ],
  materialId: [{ message: '请选择成品物料', required: true, trigger: 'change', type: 'number' }],
  orderId: [{ message: '请选择生产订单', required: true, trigger: 'change', type: 'number' }],
  qualifiedQty: [
    {
      trigger: 'change',
      validator: (_rule, value, callback) => {
        if (typeof value !== 'number' || value < 0) {
          callback(new Error('合格数量不能小于 0'))
        } else if (value > form.finishQty) {
          callback(new Error('合格数量不能大于完工数量'))
        } else {
          callback()
        }
      },
    },
  ],
  versionId: [{ message: '请选择 BOM 版本', required: true, trigger: 'change', type: 'number' }],
}

async function loadReferenceData() {
  const currentRequestId = ++referenceRequestId
  referenceLoading.value = true
  referenceError.value = ''
  try {
    const data = await inventoryService.getReferenceData()
    if (alive && currentRequestId === referenceRequestId) {
      referenceData.value = data
    }
  } catch (requestError) {
    if (alive && currentRequestId === referenceRequestId) {
      referenceError.value = getErrorMessage(requestError, '生产订单与成品选项加载失败')
    }
  } finally {
    if (alive && currentRequestId === referenceRequestId) {
      referenceLoading.value = false
    }
  }
}

async function loadItems() {
  const currentRequestId = ++requestId
  loading.value = true
  error.value = ''
  try {
    const result = await inventoryService.listCompletionInbound({
      ...query,
      endTime: dateRange.value?.[1],
      startTime: dateRange.value?.[0],
    })
    if (!alive || currentRequestId !== requestId) {
      return
    }
    items.value = result.items
    total.value = result.total
  } catch (requestError) {
    if (alive && currentRequestId === requestId) {
      error.value = getErrorMessage(requestError, '完工入库记录加载失败')
    }
  } finally {
    if (alive && currentRequestId === requestId) {
      loading.value = false
    }
  }
}

function resetQuery() {
  Object.assign(query, { materialId: undefined, orderId: undefined, page: 1 })
  dateRange.value = undefined
  void loadItems()
}

function searchItems() {
  query.page = 1
  void loadItems()
}

function openDialog() {
  Object.assign(form, {
    batchNo: '',
    finishQty: 1,
    materialId: 0,
    operatorId: auth.currentUser?.id ?? 0,
    orderId: 0,
    qualifiedQty: 1,
    versionId: 0,
  })
  dialogOpen.value = true
}

function handleProductionOrderChange(orderId: number) {
  const order = referenceData.value.productionOrders.find((item) => item.orderId === orderId)
  if (!order) {
    return
  }
  Object.assign(form, {
    finishQty: Math.min(1, order.remainingQty),
    materialId: order.materialId,
    qualifiedQty: Math.min(1, order.remainingQty),
    versionId: order.versionId,
  })
  formRef.value?.clearValidate()
}

async function viewInbound(item: CompletionInboundItem) {
  selectedInbound.value = undefined
  detailError.value = ''
  detailDrawerOpen.value = true
  detailLoading.value = true
  try {
    selectedInbound.value = await inventoryService.getCompletionInboundDetail(item.inboundId)
  } catch (requestError) {
    detailError.value = getErrorMessage(requestError, '完工入库详情加载失败')
  } finally {
    detailLoading.value = false
  }
}

function openBatchTrace() {
  const inbound = selectedInbound.value
  if (!inbound) {
    return
  }
  void router.push({
    path: '/trace/product',
    query: { batchNo: inbound.batchNo, orderId: String(inbound.orderId) },
  })
}

function closeInboundDetail() {
  selectedInbound.value = undefined
  detailError.value = ''
}

async function submitInbound() {
  const operatorId = auth.currentUser?.id
  if (!operatorId) {
    ElMessage.error('当前会话缺少操作人信息，请重新登录')
    return
  }
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid || submitting.value) {
    return
  }
  const productionOrder: InventoryProductionOrderOption | undefined = selectedProductionOrder.value
  if (
    productionOrder &&
    (productionOrder.materialId !== form.materialId || productionOrder.versionId !== form.versionId)
  ) {
    ElMessage.warning('生产订单、成品物料和 BOM 版本不匹配')
    return
  }
  if (productionOrder && form.finishQty > productionOrder.remainingQty) {
    ElMessage.warning(`本次完工数量不能超过剩余数量 ${formatNumber(productionOrder.remainingQty)}`)
    return
  }
  submitting.value = true
  try {
    const created = await inventoryService.addCompletionInbound({
      ...form,
      batchNo: form.batchNo.trim(),
      operatorId,
    })
    const consumedCount = created?.consumedLockRecords?.length ?? 0
    let message = '完工入库登记成功'
    if (consumedCount > 0) {
      message = `完工入库登记成功，已消耗 ${consumedCount} 条库存锁定记录`
    }
    ElMessage.success(message)
    dialogOpen.value = false
    query.page = 1
    await Promise.all([loadItems(), loadReferenceData()])
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '完工入库登记失败'))
  } finally {
    submitting.value = false
  }
}

onMounted(() => {
  void loadItems()
  void loadReferenceData()
})
onBeforeUnmount(() => {
  alive = false
  requestId += 1
  referenceRequestId += 1
})
</script>

<template>
  <PageContainer>
    <PageHeader
      title="完工入库登记"
      description="登记生产完工批次，核对合格数量并追踪历史入库记录。"
    >
      <template #actions>
        <el-button :icon="Refresh" :loading="loading" @click="loadItems">刷新</el-button>
        <el-button :icon="Plus" type="primary" @click="openDialog">登记入库</el-button>
      </template>
    </PageHeader>

    <el-alert
      v-if="referenceError"
      class="request-error"
      :closable="false"
      show-icon
      :title="referenceError"
      type="warning"
    >
      <template #default
        ><el-button link type="primary" @click="loadReferenceData"
          >重新加载关联选项</el-button
        ></template
      >
    </el-alert>

    <el-card class="query-card" shadow="never">
      <div class="query-bar">
        <el-select v-model="query.orderId" clearable filterable placeholder="生产订单">
          <el-option
            v-for="order in referenceData.productionOrders"
            :key="order.orderId"
            :label="`#${order.orderId} · ${order.materialName}`"
            :value="order.orderId"
          />
        </el-select>
        <el-select v-model="query.materialId" clearable filterable placeholder="成品物料">
          <el-option
            v-for="material in referenceData.materials.filter(
              (item) => item.materialType === 'finished',
            )"
            :key="material.materialId"
            :label="material.materialName"
            :value="material.materialId"
          />
        </el-select>
        <el-date-picker
          v-model="dateRange"
          end-placeholder="结束日期"
          range-separator="至"
          start-placeholder="开始日期"
          type="daterange"
          value-format="YYYY-MM-DD"
        />
        <el-button :icon="Search" type="primary" @click="searchItems">查询</el-button>
        <el-button @click="resetQuery">重置</el-button>
      </div>
    </el-card>

    <el-alert
      v-if="error"
      class="request-error"
      :closable="false"
      show-icon
      :title="error"
      type="error"
    >
      <template #default
        ><el-button link type="primary" @click="loadItems">重新加载</el-button></template
      >
    </el-alert>

    <el-card class="records-card table-card table-card--accent" shadow="never">
      <template #header
        ><div class="table-card__header"><span>入库记录</span></div></template
      >
      <div v-loading="loading" class="records-area">
        <EmptyState
          v-if="!loading && !error && !items.length"
          description="当前查询条件下没有完工入库记录。"
        />
        <el-table v-else :data="items" stripe>
          <el-table-column label="入库 ID" min-width="90" prop="inboundId" />
          <el-table-column label="批次号" min-width="170"
            ><template #default="{ row }"
              ><strong>{{ row.batchNo }}</strong></template
            ></el-table-column
          >
          <el-table-column label="生产订单" min-width="110"
            ><template #default="{ row }">#{{ row.orderId }}</template></el-table-column
          >
          <el-table-column label="成品物料" min-width="190"
            ><template #default="{ row }"
              ><div class="product-cell">
                <strong>{{ row.productName || `物料 #${row.materialId}` }}</strong
                ><small>ID {{ row.materialId }} · BOM #{{ row.versionId }}</small>
              </div></template
            ></el-table-column
          >
          <el-table-column label="完工 / 合格" min-width="140"
            ><template #default="{ row }"
              >{{ formatNumber(row.finishQty) }} /
              <strong class="qualified">{{ formatNumber(row.qualifiedQty) }}</strong></template
            ></el-table-column
          >
          <el-table-column label="合格率" min-width="100"
            ><template #default="{ row }">{{
              row.finishQty ? `${formatNumber((row.qualifiedQty / row.finishQty) * 100)}%` : '-'
            }}</template></el-table-column
          >
          <el-table-column label="消耗锁定" min-width="100"
            ><template #default="{ row }">{{
              row.consumedLockRecords?.length ?? 0
            }}</template></el-table-column
          >
          <el-table-column label="入库时间" min-width="175"
            ><template #default="{ row }">{{
              formatDateTime(row.inboundTime)
            }}</template></el-table-column
          >
          <el-table-column label="操作人" min-width="90"
            ><template #default="{ row }">#{{ row.operatorId }}</template></el-table-column
          >
          <el-table-column fixed="right" label="操作" min-width="90">
            <template #default="{ row }">
              <el-button link type="primary" @click="viewInbound(row)">详情</el-button>
            </template>
          </el-table-column>
        </el-table>
      </div>
      <el-pagination
        v-if="total"
        v-model:current-page="query.page"
        v-model:page-size="query.pageSize"
        :page-sizes="[10, 20, 50]"
        background
        layout="total, sizes, prev, pager, next"
        :total="total"
        @change="loadItems"
      />
    </el-card>

    <el-dialog
      v-model="dialogOpen"
      title="登记完工入库"
      width="min(94vw, 620px)"
      @closed="formRef?.resetFields()"
    >
      <el-form ref="formRef" :model="form" :rules="formRules" label-width="115px">
        <div class="form-grid">
          <el-form-item label="生产订单" prop="orderId"
            ><el-input-number
              :controls="false"
              v-if="!referenceData.productionOrders.length"
              v-model="form.orderId"
              :min="1"
              :precision="0"
              placeholder="输入生产订单编号" /><el-select
              v-else
              v-model="form.orderId"
              filterable
              :loading="referenceLoading"
              placeholder="选择待入库订单"
              @change="handleProductionOrderChange"
              ><el-option
                v-for="order in inboundOrderOptions"
                :key="order.orderId"
                :label="`#${order.orderId} · ${order.materialName} · 剩余 ${formatNumber(
                  order.remainingQty,
                )}`"
                :value="order.orderId" /></el-select
          ></el-form-item>
          <el-form-item label="成品物料" prop="materialId"
            ><el-input-number
              :controls="false"
              v-if="!referenceData.productionOrders.length"
              v-model="form.materialId"
              :min="1"
              :precision="0"
              placeholder="输入成品物料编号" /><el-select v-else v-model="form.materialId" disabled
              ><el-option
                v-if="selectedProductionOrder"
                :label="selectedProductionOrder.materialName"
                :value="selectedProductionOrder.materialId" /></el-select
          ></el-form-item>
          <el-form-item label="BOM 版本" prop="versionId"
            ><el-input-number
              :controls="false"
              v-if="!referenceData.productionOrders.length"
              v-model="form.versionId"
              :min="1"
              :precision="0"
              placeholder="输入 BOM 版本编号" /><el-select v-else v-model="form.versionId" disabled
              ><el-option
                v-if="selectedProductionOrder"
                :label="
                  selectedProductionOrder.versionNo || `#${selectedProductionOrder.versionId}`
                "
                :value="selectedProductionOrder.versionId" /></el-select
          ></el-form-item>
          <el-form-item label="生产批次号" prop="batchNo"
            ><el-input v-model.trim="form.batchNo" maxlength="80" placeholder="如 AX100-20260727-A"
          /></el-form-item>
          <el-form-item label="完工数量" prop="finishQty"
            ><el-input-number
              :controls="false"
              v-model="form.finishQty"
              :max="selectedProductionOrder?.remainingQty"
              :min="0.01"
              :precision="2"
          /></el-form-item>
          <el-form-item label="合格数量" prop="qualifiedQty"
            ><el-input-number
              :controls="false"
              v-model="form.qualifiedQty"
              :max="form.finishQty"
              :min="0"
              :precision="2"
          /></el-form-item>
        </div>
        <el-alert
          :closable="false"
          :title="`本批次合格率 ${formatNumber(qualifiedRate)}%${
            selectedProductionOrder
              ? ` · 订单剩余 ${formatNumber(selectedProductionOrder.remainingQty)}`
              : ''
          }`"
          type="info"
        />
      </el-form>
      <template #footer
        ><el-button @click="dialogOpen = false">取消</el-button
        ><el-button :loading="submitting" type="primary" @click="submitInbound"
          >确认登记</el-button
        ></template
      >
    </el-dialog>

    <el-drawer
      v-model="detailDrawerOpen"
      size="min(94vw, 680px)"
      title="完工入库详情"
      @closed="closeInboundDetail"
    >
      <div v-loading="detailLoading" class="inbound-detail-area">
        <el-alert
          v-if="detailError"
          :closable="false"
          show-icon
          :title="detailError"
          type="error"
        />
        <template v-else-if="selectedInbound">
          <div class="inbound-detail-grid">
            <div>
              <span>入库单</span><strong>#{{ selectedInbound.inboundId }}</strong>
            </div>
            <div>
              <span>生产订单</span
              ><strong>{{
                selectedInbound.productionOrder
                  ? '#' +
                    selectedInbound.productionOrder.orderId +
                    ' · ' +
                    selectedInbound.productionOrder.materialName
                  : '#' + selectedInbound.orderId
              }}</strong>
            </div>
            <div>
              <span>成品</span><strong>{{ selectedInbound.productName }}</strong>
            </div>
            <div>
              <span>批次号</span><strong>{{ selectedInbound.batchNo }}</strong>
            </div>
            <div>
              <span>完工数量</span><strong>{{ formatNumber(selectedInbound.finishQty) }}</strong>
            </div>
            <div>
              <span>合格数量</span><strong>{{ formatNumber(selectedInbound.qualifiedQty) }}</strong>
            </div>
            <div>
              <span>BOM 版本</span
              ><strong>{{
                selectedInbound.bomVersionNo || '#' + selectedInbound.versionId
              }}</strong>
            </div>
            <div>
              <span>操作人</span><strong>#{{ selectedInbound.operatorId }}</strong>
            </div>
          </div>
          <div class="detail-actions">
            <el-button :icon="Search" type="primary" @click="openBatchTrace">批次追溯</el-button>
          </div>
          <el-divider content-position="left">原料锁定消耗</el-divider>
          <EmptyState
            v-if="!selectedInbound.consumedLockRecords?.length"
            description="该入库记录没有返回原料锁定消耗明细。"
          />
          <el-table v-else :data="selectedInbound.consumedLockRecords" stripe>
            <el-table-column label="锁定 ID" prop="lockId" min-width="90" />
            <el-table-column label="物料" min-width="180">
              <template #default="{ row }">{{
                row.materialName || `物料 #${row.materialId}`
              }}</template>
            </el-table-column>
            <el-table-column label="消耗数量" min-width="110">
              <template #default="{ row }">{{ formatNumber(row.lockQty) }}</template>
            </el-table-column>
          </el-table>
        </template>
      </div>
    </el-drawer>
  </PageContainer>
</template>

<style scoped>
.query-card,
.request-error {
  margin-bottom: 16px;
}

.detail-actions {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
.inbound-detail-area {
  min-height: 180px;
}
.query-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px;
}
.query-bar :deep(.el-input-number) {
  width: 155px;
}
.query-bar :deep(.el-select) {
  width: 190px;
}
.records-area {
  min-height: 260px;
}
.product-cell {
  display: grid;
  gap: 2px;
}
.product-cell small {
  color: var(--el-text-color-secondary);
}
.qualified {
  color: var(--el-color-success);
}
:deep(.el-pagination) {
  justify-content: flex-end;
  margin-top: 16px;
}
.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  column-gap: 12px;
}
.form-grid :deep(.el-input-number),
.form-grid :deep(.el-input),
.form-grid :deep(.el-select) {
  width: 100%;
}
.inbound-detail-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}
.inbound-detail-grid > div {
  display: grid;
  gap: 4px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  padding: 12px;
}
.inbound-detail-grid span {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
@media (max-width: 680px) {
  .query-bar > * {
    flex: 1 1 160px;
  }
  .form-grid {
    grid-template-columns: 1fr;
  }
  .inbound-detail-grid {
    grid-template-columns: 1fr;
  }
}
</style>
